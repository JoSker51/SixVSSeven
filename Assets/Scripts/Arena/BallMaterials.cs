using UnityEngine;

// Cómo sale la pelota al chocar un objeto según su material. Lo llama Rock en OnCollisionEnter,
// cuando PhysX ya hizo el rebote: aquí solo se corrige la velocidad de salida.
//  - Liso (positivos y negativos): rebote normal, un poco más rápido y algo desviado al azar
//  - Goma: rebota con mucha más fuerza
//  - Turbo: sale casi al doble de rápido en la dirección del rebote, un poco hacia arriba
//  - Bumper: la empuja siempre hacia afuera a la misma velocidad, como en el pinball
public static class BallMaterials
{
    public static string Name(ItemMaterial m) => m switch
    {
        ItemMaterial.Rubber => "Goma",
        ItemMaterial.Turbo => "Turbo",
        ItemMaterial.Bumper => "Bumper",
        _ => "Liso",
    };

    // Color de la estela, las chispas y la etiqueta de la tienda
    public static Color Tint(ItemMaterial m) => m switch
    {
        ItemMaterial.Rubber => new Color(1f, 0.42f, 0.72f),
        ItemMaterial.Turbo => new Color(1f, 0.58f, 0.12f),
        ItemMaterial.Bumper => new Color(0.35f, 0.9f, 1f),
        _ => new Color(0.85f, 0.95f, 1f),
    };

    public static Sfx Sound(ItemMaterial m) => m switch
    {
        ItemMaterial.Rubber => Sfx.Rubber,
        ItemMaterial.Turbo => Sfx.Turbo,
        ItemMaterial.Bumper => Sfx.Bumper,
        _ => Sfx.Hover,
    };

    // Devuelve true si el choque tuvo fuerza suficiente para notarse (chispas, sonido, temblor)
    public static bool Apply(Rigidbody rb, ArenaObject obj, Collision collision, GameBalance bal)
    {
        Vector3 v = rb.linearVelocity;
        float incoming = collision.relativeVelocity.magnitude;   // velocidad con la que llegó (antes del rebote)
        float speed = v.magnitude;
        if (collision.contactCount == 0) return false;

        // La normal que apunta del objeto hacia la pelota
        Vector3 n = collision.GetContact(0).normal;
        Vector3 away = rb.position - collision.GetContact(0).point;
        if (Vector3.Dot(n, away) < 0f) n = -n;

        Vector3 dir = speed > 0.05f ? v / speed : Vector3.Reflect(-collision.relativeVelocity.normalized, n);
        if (Vector3.Dot(dir, n) < 0f) dir = Vector3.Reflect(dir, n);   // nunca hacia dentro del objeto

        float cap = bal.maxBounceSpeed;
        switch (obj.Material)
        {
            case ItemMaterial.Rubber:
                if (incoming < 2f) return false;   // rodando encima: no se empuja
                dir = Outward(Scatter(dir, bal.rubberScatter), n);
                rb.linearVelocity = dir * Mathf.Min(incoming * bal.rubberBoost, cap);
                return true;

            case ItemMaterial.Turbo:
                dir = Outward((dir + Vector3.up * bal.turboLift).normalized, n);
                rb.linearVelocity = dir * Mathf.Min(Mathf.Max(incoming * bal.turboBoost, bal.turboMinSpeed), cap);
                return true;

            case ItemMaterial.Bumper:
                Vector3 tangent = Vector3.ProjectOnPlane(-collision.relativeVelocity, n) * bal.bumperKeepTangent;
                Vector3 push = (n + Vector3.up * bal.bumperUp).normalized * bal.bumperSpeed;
                Vector3 outV = push + tangent;
                rb.linearVelocity = Outward(outV.normalized, n) * Mathf.Min(outV.magnitude, cap);
                obj.Punch();
                return true;

            default:
                if (speed < 2f) return false;
                dir = Outward(Scatter(dir, bal.bounceScatter), n);
                rb.linearVelocity = dir * Mathf.Min(speed * bal.bounceBoost, Mathf.Max(speed, cap));
                return false;   // el liso no hace chispas: así los materiales se distinguen
        }
    }

    private static Vector3 Scatter(Vector3 dir, float amount) =>
        amount <= 0f ? dir : Vector3.Slerp(dir, Random.onUnitSphere, amount).normalized;

    private static Vector3 Outward(Vector3 dir, Vector3 n) => Vector3.Dot(dir, n) < 0f ? Vector3.Reflect(dir, n) : dir;
}
