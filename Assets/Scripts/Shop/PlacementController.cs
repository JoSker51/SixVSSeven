using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum PlaceResult { Ok, WrongPhase, NotInInventory, Invalid }
public enum RemoveResult { Ok, WrongPhase, NoTarget, NotEnoughCoins }

// Colocación y retirada de objetos, solo en la fase de Colocación del propio turno.
//  - Colocar: se elige un objeto del inventario y se ve un "fantasma" de vidrio que sigue al mouse.
//    El fantasma "cae" hasta apoyarse en el suelo o encima de otro objeto, así se pueden armar estructuras.
//    Q/E gira, R/F inclina, Z/X ladea, Shift + ruedita (o RePág/AvPág) lo sube para dejarlo flotando.
//    Clic confirma; clic derecho corto o Esc cancela (el objeto vuelve a estar disponible).
//    Puede tocar a otros objetos pero no atravesarlos; tampoco salir del círculo, atravesar el suelo ni
//    encimarse con burbujas, paredes, castillos o catapultas.
//  - Retirar: clic sobre cualquier objeto colocado, se muestra el costo y hay que confirmar. No vuelve al inventario.
//    Lo que estaba apoyado encima cae hasta lo que quede debajo.
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

    private const int Mask = ~(1 << 2);              // todo menos "Ignore Raycast" (fantasma y aura)
    private const float Shrink = 0.02f;              // la caja de prueba es un poco más chica: tocarse no es chocar
    private const float RestGap = 0.03f;             // al apoyar, la caja chica queda así de separada (la real, ~1 cm)
    private const float TouchTolerance = 0.03f;

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
        placed.Resting = DropDistance(placed) < 0.05f;   // apoyado: si se retira lo de abajo, cae
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
        if (maxY > Bal.stackMaxHeight) { reason = "Demasiado alto"; return false; }
        if (arena.InKeepOut(center, footprint * 0.6f)) { reason = "Muy cerca de un castillo o una catapulta"; return false; }

        foreach (var b in Bubble.All)
        {
            if (!b || b.Popped || !b.isActiveAndEnabled) continue;
            if (DistanceToBox(b.transform.position, center, rotation, half) < b.Radius + Bal.bubbleObjectClearance)
            { reason = "Choca con una burbuja"; return false; }
        }

        foreach (var c in Physics.OverlapBox(center, half + Vector3.one * Bal.placementClearance, rotation, Mask, QueryTriggerInteraction.Ignore))
        {
            if (c.CompareTag("Ground") || c.GetComponentInParent<ArenaObject>()) continue;
            reason = "Choca con la pared, un castillo o una catapulta";
            return false;
        }
        // Con otros objetos se puede tocar (apoyarse encima o al lado), pero no atravesarlos
        foreach (var c in Physics.OverlapBox(center, Shrunk(half, TouchTolerance), rotation, Mask, QueryTriggerInteraction.Ignore))
        {
            var other = c.GetComponentInParent<ArenaObject>();
            if (other != null && other.IsPlaced) { reason = "Choca con otro objeto"; return false; }
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
        Settle();
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
        else if (RemovalTarget != null && (GameInput.RightTap || GameInput.Pressed(Key.Escape)))
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
        // Altura: Shift + ruedita (la ruedita sola acerca o aleja la cámara)
        int scroll = (GameInput.ShiftHeld ? GameInput.ScrollStep : 0) + (GameInput.Pressed(Key.PageUp) ? 1 : 0) - (GameInput.Pressed(Key.PageDown) ? 1 : 0);
        height = Mathf.Clamp(height + scroll * Bal.heightStep, 0f, Bal.placementMaxHeight);

        var ray = cam.ScreenPointToRay(GameInput.MousePosition);
        if (Physics.Raycast(ray, out var hit, 500f, Mask, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ArenaObject>() != null)
            floorPoint = hit.point;   // apuntando a un objeto: el nuevo se apila ahí
        else if (Mathf.Abs(ray.direction.y) > 1e-4f)
        {
            float t = -ray.origin.y / ray.direction.y;
            if (t > 0f) floorPoint = ray.origin + ray.direction * t;
        }

        Quaternion rot = Quaternion.Euler(euler);
        Vector3 pos = PoseOnSupport(Holding, floorPoint, height, rot);
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
        else if (GameInput.RightTap || GameInput.Pressed(Key.Escape))   // clic derecho corto: arrastrar gira la cámara
        {
            if (GameInput.Pressed(Key.Escape)) GameInput.ConsumeEscape();
            CancelPlacing();
        }
    }

    // Posición del objeto "dejándolo caer" en ese punto: queda apoyado en el suelo o encima del objeto que
    // tenga debajo; 'lift' lo sube a partir de ahí para dejarlo flotando
    public Vector3 PoseOnSupport(ItemDefinition item, Vector3 point, float lift, Quaternion rot)
    {
        Vector3 pos = PoseOnFloor(item, point, 0f, rot);
        Bounds lb = LocalBounds(item);
        Vector3 center = pos + rot * lb.center;
        float top = Bal.stackMaxHeight + 5f;
        float raise = 0f;
        foreach (var h in Physics.BoxCastAll(center + Vector3.up * top, Shrunk(lb.extents, Shrink), Vector3.down, rot, top, Mask, QueryTriggerInteraction.Ignore))
        {
            if (h.distance <= 0f) continue;
            var obj = h.collider.GetComponentInParent<ArenaObject>();
            if (obj == null || !obj.IsPlaced) continue;   // solo se apila sobre objetos (el suelo es la base)
            raise = Mathf.Max(raise, top - h.distance + RestGap);
        }
        pos.y += raise + lift;
        return pos;
    }

    // Cuánto puede bajar un objeto colocado hasta apoyarse en el suelo o en otro objeto
    public float DropDistance(ArenaObject obj)
    {
        Bounds lb = LocalBounds(obj.Definition);
        Quaternion rot = obj.transform.rotation;
        Vector3 center = obj.transform.position + rot * lb.center;
        float drop = obj.transform.position.y - PoseOnFloor(obj.Definition, obj.transform.position, 0f, rot).y;   // hasta el suelo
        foreach (var h in Physics.BoxCastAll(center, Shrunk(lb.extents, Shrink), Vector3.down, rot, drop + 1f, Mask, QueryTriggerInteraction.Ignore))
        {
            if (h.distance <= 0f || h.collider.transform.IsChildOf(obj.transform)) continue;
            var other = h.collider.GetComponentInParent<ArenaObject>();
            if (other == null || !other.IsPlaced) continue;
            drop = Mathf.Min(drop, h.distance - RestGap);
        }
        return Mathf.Max(0f, drop);
    }

    // Después de retirar algo: lo que estaba apoyado cae, de abajo hacia arriba (así una torre baja entera).
    // Lo que se dejó flotando a propósito (con Shift + ruedita) se queda donde está.
    private void Settle()
    {
        var falls = new List<(ArenaObject obj, Vector3 from, Vector3 to)>();
        foreach (var o in ArenaObject.All.Where(o => o && o.IsPlaced && o.Resting).OrderBy(o => o.transform.position.y).ToList())
        {
            float d = DropDistance(o);
            if (d < 0.02f) continue;
            Vector3 from = o.transform.position, to = from + Vector3.down * d;
            o.transform.position = to;
            Physics.SyncTransforms();
            falls.Add((o, from, to));
        }
        foreach (var f in falls) f.obj.AnimateFall(f.from, f.to);
    }

    private static Vector3 Shrunk(Vector3 half, float by) => Vector3.Max(half - Vector3.one * by, Vector3.one * 0.01f);

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
