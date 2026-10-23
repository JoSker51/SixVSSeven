using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum PlaceResult { Ok, WrongPhase, NotInInventory, Invalid }
public enum RemoveResult { Ok, WrongPhase, NoTarget, NotEnoughCoins }

// Colocación y retirada de objetos, solo en la fase de Colocación del propio turno.
//  - Colocar: se elige un objeto del inventario y se ve un "fantasma" de vidrio que sigue al mouse.
//    Q/E gira, R/F inclina, Z/X ladea, la ruedita sube o baja. Clic confirma; clic derecho o Esc cancela
//    (el objeto vuelve a estar disponible). No se puede salir del círculo, atravesar el suelo ni encimarse
//    con burbujas, paredes, castillos, catapultas u otros objetos.
//  - Retirar: clic sobre cualquier objeto colocado, se muestra el costo y hay que confirmar. No vuelve al inventario.
[DefaultExecutionOrder(-10)]
public class PlacementController : MonoBehaviour
{
    public static PlacementController Instance { get; private set; }

    [SerializeField] private Material ghostValid;
    [SerializeField] private Material ghostInvalid;
    [SerializeField] private Material selectionHalo;

    public ItemDefinition Holding { get; private set; }
    public bool IsValid { get; private set; }
    public string Reason { get; private set; }
    public ArenaObject RemovalTarget { get; private set; }
    public string LastMessage { get; private set; }

    public event Action Changed;

    private GameObject ghost;
    private GameObject halo;
    private Vector3 euler;
    private float height;
    private Vector3 floorPoint;
    private bool lastValid;
    private readonly Dictionary<string, Bounds> boundsCache = new Dictionary<string, Bounds>();
    private Transform placedRoot;
    private Camera cam;

    private static GameBalance Bal => GameBalance.Current;
    private static TurnManager Turns => TurnManager.Instance;

    private void Awake() => Instance = this;

    private void Start()
    {
        placedRoot = new GameObject("Objetos colocados").transform;
        if (Turns != null) Turns.PhaseChanged += OnPhaseChanged;
    }

    private void OnDestroy()
    {
        if (Turns != null) Turns.PhaseChanged -= OnPhaseChanged;
        if (Instance == this) Instance = null;
    }

    private void OnPhaseChanged(TurnPhase phase)
    {
        if (phase != TurnPhase.Placement) { CancelPlacing(); CancelRemoval(); }
    }

    // ---------------- Colocar ----------------

    public bool BeginPlacing(ItemDefinition item)
    {
        int p = Turns ? Turns.CurrentPlayer : -1;
        if (Turns == null || !Turns.IsPhase(TurnPhase.Placement, p)) return false;
        var inv = MatchSetup.Instance.Player(p).Inventory;
        if (item == null || !inv.Contains(item)) return false;

        CancelPlacing();
        CancelRemoval();
        Holding = item;
        euler = item.kind == ItemKind.Ring ? new Vector3(90f, 0f, 0f) : Vector3.zero;   // el aro empieza de pie
        height = item.kind == ItemKind.Ring ? 1.5f : 0f;
        ghost = MakeCopy(item.prefab, ghostValid, "Fantasma");
        lastValid = true;
        LastMessage = null;
        Changed?.Invoke();
        return true;
    }

    // Cancelar no consume el objeto: sigue en el inventario
    public void CancelPlacing()
    {
        if (ghost) Destroy(ghost);
        ghost = null;
        if (Holding != null) { Holding = null; Changed?.Invoke(); }
    }

    // Coloca directamente en una posición (lo usa la colocación con mouse y las pruebas)
    public PlaceResult TryPlace(ItemDefinition item, Vector3 position, Quaternion rotation, out ArenaObject placed)
    {
        placed = null;
        int p = Turns ? Turns.CurrentPlayer : -1;
        if (Turns == null || !Turns.IsPhase(TurnPhase.Placement, p)) return PlaceResult.WrongPhase;
        var inv = MatchSetup.Instance.Player(p).Inventory;
        if (item == null || !inv.Contains(item)) return PlaceResult.NotInInventory;
        if (!Validate(item, position, rotation, out string reason)) { Reason = reason; return PlaceResult.Invalid; }

        inv.Remove(item);
        var go = Instantiate(item.prefab, position, rotation, placedRoot);
        go.name = $"{item.FullName} (J{p + 1})";
        placed = go.GetComponent<ArenaObject>();
        placed.Init(item, p);
        Physics.SyncTransforms();
        AeroAudio.Play(Sfx.Place);
        Changed?.Invoke();
        return PlaceResult.Ok;
    }

    public bool Validate(ItemDefinition item, Vector3 position, Quaternion rotation, out string reason)
    {
        var arena = Arena.Instance;
        Bounds lb = LocalBounds(item);
        Vector3 center = position + rotation * lb.center;
        Vector3 half = lb.extents;

        float minY = float.MaxValue, maxY = float.MinValue, footprint = 0f;
        bool inside = true;
        for (int i = 0; i < 8; i++)
        {
            var corner = center + rotation * Vector3.Scale(half, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            minY = Mathf.Min(minY, corner.y);
            maxY = Mathf.Max(maxY, corner.y);
            inside &= arena.InsideHorizontal(corner, 0.25f);
            footprint = Mathf.Max(footprint, new Vector2(corner.x - center.x, corner.z - center.z).magnitude);
        }
        if (!inside) { reason = "Fuera de la arena"; return false; }
        if (minY < -0.01f) { reason = "Atraviesa el suelo"; return false; }
        if (maxY > Bal.placementMaxHeight + 3f) { reason = "Demasiado alto"; return false; }
        if (arena.InKeepOut(center, footprint * 0.6f)) { reason = "Muy cerca de un castillo o una catapulta"; return false; }

        foreach (var b in Bubble.All)
        {
            if (!b || b.Popped || !b.isActiveAndEnabled) continue;
            if (DistanceToBox(b.transform.position, center, rotation, half) < b.Radius + Bal.bubbleObjectClearance)
            { reason = "Choca con una burbuja"; return false; }
        }

        foreach (var c in Physics.OverlapBox(center, half + Vector3.one * Bal.placementClearance, rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.CompareTag("Ground")) continue;
            if (ghost && c.transform.IsChildOf(ghost.transform)) continue;
            if (halo && c.transform.IsChildOf(halo.transform)) continue;
            reason = c.GetComponentInParent<ArenaObject>() ? "Choca con otro objeto" : "Choca con la pared, un castillo o una catapulta";
            return false;
        }
        reason = null;
        return true;
    }

    // ---------------- Retirar ----------------

    public bool SelectForRemoval(ArenaObject obj)
    {
        int p = Turns ? Turns.CurrentPlayer : -1;
        if (Turns == null || !Turns.IsPhase(TurnPhase.Placement, p) || obj == null || !obj.IsPlaced) return false;
        CancelRemoval();
        RemovalTarget = obj;
        halo = MakeCopy(obj.Definition.prefab, selectionHalo, "Seleccion");
        halo.transform.SetPositionAndRotation(obj.transform.position, obj.transform.rotation);
        halo.transform.localScale = Vector3.one * 1.06f;
        LastMessage = null;
        Changed?.Invoke();
        return true;
    }

    public int RemovalCost(ArenaObject obj) => obj != null && obj.Definition != null ? obj.Definition.removalCost : 0;

    public bool CanAffordRemoval
    {
        get
        {
            int p = Turns ? Turns.CurrentPlayer : -1;
            return RemovalTarget != null && GameManager.Instance.GetCoins(p) >= RemovalCost(RemovalTarget);
        }
    }

    // Cobra, quita el objeto de la arena (deja de colisionar en este mismo instante) y limpia sus referencias
    public RemoveResult ConfirmRemoval()
    {
        int p = Turns ? Turns.CurrentPlayer : -1;
        if (Turns == null || !Turns.IsPhase(TurnPhase.Placement, p)) return RemoveResult.WrongPhase;
        var obj = RemovalTarget;
        if (obj == null || !obj.IsPlaced) return RemoveResult.NoTarget;
        int cost = RemovalCost(obj);
        if (!GameManager.Instance.SpendCoins(p, cost))
        {
            LastMessage = "No te alcanzan las monedas";
            AeroAudio.Play(Sfx.Error);
            Changed?.Invoke();
            return RemoveResult.NotEnoughCoins;
        }
        Vector3 at = obj.transform.position;
        string name = obj.Definition.FullName;
        CancelRemoval();
        obj.RemoveFromArena();
        Physics.SyncTransforms();
        AeroAudio.Play(Sfx.Pop);
        GameHUD.Popup(at, $"−{cost}", new Color(1f, 0.55f, 0.6f));
        LastMessage = $"Retiraste {name}";
        Changed?.Invoke();
        return RemoveResult.Ok;
    }

    public void CancelRemoval()
    {
        if (halo) Destroy(halo);
        halo = null;
        if (RemovalTarget != null) { RemovalTarget = null; Changed?.Invoke(); }
    }

    // ---------------- Mouse y teclado ----------------

    private void Update()
    {
        if (Turns == null || Turns.Phase != TurnPhase.Placement || Time.timeScale == 0f) return;
        if (!cam) cam = Camera.main;
        if (!cam) return;

        if (Holding != null) UpdateGhost();
        else if (GameInput.LeftClick && !PointerOverUI())
        {
            var ray = cam.ScreenPointToRay(GameInput.MousePosition);
            if (Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                var obj = hit.collider.GetComponentInParent<ArenaObject>();
                if (obj != null && obj.IsPlaced) SelectForRemoval(obj);
            }
        }
        else if (RemovalTarget != null && (GameInput.RightClick || GameInput.Pressed(Key.Escape)))
        {
            if (GameInput.Pressed(Key.Escape)) GameInput.ConsumeEscape();
            CancelRemoval();
        }
    }

    private void UpdateGhost()
    {
        float step = Bal.rotationStep;
        if (GameInput.Pressed(Key.Q)) euler.y -= step;
        if (GameInput.Pressed(Key.E)) euler.y += step;
        if (GameInput.Pressed(Key.R)) euler.x += step;
        if (GameInput.Pressed(Key.F)) euler.x -= step;
        if (GameInput.Pressed(Key.Z)) euler.z += step;
        if (GameInput.Pressed(Key.X)) euler.z -= step;
        int scroll = GameInput.ScrollStep + (GameInput.Pressed(Key.PageUp) ? 1 : 0) - (GameInput.Pressed(Key.PageDown) ? 1 : 0);
        height = Mathf.Clamp(height + scroll * Bal.heightStep, 0f, Bal.placementMaxHeight);

        var ray = cam.ScreenPointToRay(GameInput.MousePosition);
        if (Mathf.Abs(ray.direction.y) > 1e-4f)
        {
            float t = -ray.origin.y / ray.direction.y;
            if (t > 0f) floorPoint = ray.origin + ray.direction * t;
        }

        Quaternion rot = Quaternion.Euler(euler);
        Vector3 pos = PoseOnFloor(Holding, floorPoint, height, rot);
        ghost.transform.SetPositionAndRotation(pos, rot);

        IsValid = Validate(Holding, pos, rot, out string reason);
        if (reason != Reason || IsValid != lastValid)
        {
            Reason = reason;
            lastValid = IsValid;
            SetMaterial(ghost, IsValid ? ghostValid : ghostInvalid);
            Changed?.Invoke();
        }

        if (GameInput.LeftClick && !PointerOverUI())
        {
            if (IsValid)
            {
                var item = Holding;
                CancelPlacing();
                TryPlace(item, pos, rot, out _);
            }
            else AeroAudio.Play(Sfx.Error);
        }
        else if (GameInput.RightClick || GameInput.Pressed(Key.Escape))
        {
            if (GameInput.Pressed(Key.Escape)) GameInput.ConsumeEscape();
            CancelPlacing();
        }
    }

    // Posición del objeto para que su punto más bajo quede a 'height' sobre el suelo
    public Vector3 PoseOnFloor(ItemDefinition item, Vector3 floor, float lift, Quaternion rot)
    {
        Bounds lb = LocalBounds(item);
        float minY = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            var corner = rot * (lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            minY = Mathf.Min(minY, corner.y);
        }
        return new Vector3(floor.x, lift - minY, floor.z);
    }

    // Caja que envuelve la forma (sin contar el símbolo decorativo), en coordenadas del objeto
    public Bounds LocalBounds(ItemDefinition item)
    {
        if (boundsCache.TryGetValue(item.id, out var b)) return b;
        bool first = true;
        var root = item.prefab.transform;
        foreach (var mf in item.prefab.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.name == "Simbolo" || (mf.transform.parent && mf.transform.parent.name == "Simbolo")) continue;
            Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (first) { b = new Bounds(c, Vector3.zero); first = false; }
                else b.Encapsulate(c);
            }
        }
        boundsCache[item.id] = b;
        return b;
    }

    // Distancia de un punto a una caja orientada (0 si está adentro)
    private static float DistanceToBox(Vector3 point, Vector3 center, Quaternion rotation, Vector3 half)
    {
        Vector3 local = Quaternion.Inverse(rotation) * (point - center);
        Vector3 clamped = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
        return (local - clamped).magnitude;
    }

    // Copia sin física ni lógica, con un material de vidrio (fantasma de colocación o aura de selección)
    private GameObject MakeCopy(GameObject prefab, Material mat, string name)
    {
        var go = Instantiate(prefab);
        go.name = name;
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var a in go.GetComponentsInChildren<ArenaObject>()) a.enabled = false;
        foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;   // Ignore Raycast
        SetMaterial(go, mat);
        return go;
    }

    private static void SetMaterial(GameObject go, Material mat)
    {
        if (!go || !mat) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
