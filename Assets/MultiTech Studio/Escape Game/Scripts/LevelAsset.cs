using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiTechStudio.EscapeGame
{
    [CreateAssetMenu(fileName = "LevelAsset", menuName = "Tools/MultiTech Studio/Escape Game Level")]
    /// <summary>
    /// ScriptableObject container for a single level's layout and metadata.
    /// </summary>
    public class LevelAsset : ScriptableObject
    {
        public string levelName = "Level";

        public GridDef grid = new GridDef();

        // Arrow definitions that will be instantiated for this level.
        public List<ArrowLineDef> arrows = new();

        public float camSize = 22f;
        // Meta information for editor tooling.
        public int arrowCount;
        public int dotsCount;
        public int totalPathPoints;
        public int coinsEarned = 10;
        public LevelDifficulty levelDifficulty = LevelDifficulty.Easy;
    }

    public enum LevelDifficulty
    {
        Easy,
        Medium,
        Hard,
        VeryHard
    }

    [Serializable]
    /// <summary>
    /// Serializable container for grid dimensions and spacing.
    /// </summary>
    public class GridDef
    {
        public int width = 10;
        public int height = 10;
        public float spacing = 1f;
        public Vector2 origin = Vector2.zero;
    }

    [Serializable]
    /// <summary>
    /// Serializable representation of an arrow line path and its runtime settings.
    /// </summary>
    public class ArrowLineDef
    {
        public List<Vector2Int> path = new();

        public float stepTime = 0.18f;
        public float zOffset = -1f;
        public bool occupyAllNodes = true;
        public float headZ = -1f;
        public Color lineColor = Color.black;
        public Color hitColor = Color.red;

        public int startIndex = 0;
    }
}
