using System.Collections.Generic;
using UnityEngine;

namespace MultiTechStudio.EscapeGame
{
    [CreateAssetMenu(fileName = "ThemeData", menuName = "Tools/MultiTech Studio/Escape Game Theme Data")]
    /// <summary>
    /// Defines cosmetic overrides for the escape game template.
    /// </summary>
    public class ThemeData : ScriptableObject
    {
        [Header("Theme Identity")]
        public string themeName = "New Theme";
        public Sprite themeIcon;

        [Header("Arrow Settings")]
        public GameObject arrowPrefab;

        [Header("Color Settings")]
        public bool haveDifferentColors = false;
        public List<Color> colors = new List<Color>();

        [Header("Visual Settings")]
        public Material skyboxMaterial;
        public Sprite gridIcon;
        public Sprite background;

        [Header("Shop Settings")]
        [Tooltip("Price in coins to purchase this theme. Set to 0 for free/unlocked themes.")]
        public int price = 100;
    }
}
