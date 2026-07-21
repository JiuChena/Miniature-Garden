using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Demo._16___Complex_Visibility_Sample.Scripts {
    [Serializable]
    public struct SurfaceDefinition {
        public string name;
        public Color baseColor;
        public List<GameObject> props;
        public float heightStart;
    }
}