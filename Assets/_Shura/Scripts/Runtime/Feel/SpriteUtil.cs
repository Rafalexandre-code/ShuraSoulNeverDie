using UnityEngine;

namespace Shura
{
    /// <summary>Sorting layers as plain orders: everything is one SpriteRenderer layer.</summary>
    public static class Sorting
    {
        public const int Floor = -2000;
        public const int Ground = -1500;
        public const int Shadow = -1000;
        public const int Overlay = 1000;
        public const int Vfx = 2000;
    }

    public static class SpriteUtil
    {
        public static SpriteRenderer Make(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
