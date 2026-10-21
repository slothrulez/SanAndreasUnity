using UnityEngine;

namespace Kochi.World
{
    /// <summary>
    /// Shared placeholder material. Primitives created at runtime otherwise use Unity's
    /// Default-Material, whose shader is stripped from player builds (renders magenta).
    /// </summary>
    public static class KochiMaterials
    {
        public const string PlaceholderPath = "Kochi/Materials/KochiPlaceholder";

        static Material _placeholder;

        public static Material Placeholder
        {
            get
            {
                if (_placeholder == null)
                    _placeholder = Resources.Load<Material>(PlaceholderPath);
                return _placeholder;
            }
        }

        public static void ApplyPlaceholder(Renderer renderer, MaterialPropertyBlock block, Color color)
        {
            var material = Placeholder;
            if (material != null)
                renderer.sharedMaterial = material;
            block.Clear();
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
    }
}
