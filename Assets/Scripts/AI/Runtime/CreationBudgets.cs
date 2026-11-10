using System;
using UnityEngine;

namespace RuntimeCreation {
    [Serializable]
    public sealed class CreationBudgets {
        [Range(1,512)] public int objects = 128;
        [Range(1,256)] public int bindings = 64;
        [Range(1,1024)] public int actions = 256;
        [Range(1,32)] public int depth = 12;
        [Range(1,64)] public int sessions = 16;
        [Range(1,1024)] public int pendingActions = 128;
        [Range(1,2048)] public int dispatchPerFrame = 256;
        public void Validate() {
            if (objects<1 || objects>512 || bindings<1 || bindings>256 || actions<1 || actions>1024 || depth<1 || depth>32 || sessions<1 || sessions>64 || pendingActions<1 || pendingActions>1024 || dispatchPerFrame<1 || dispatchPerFrame>2048) throw new ArgumentException("Creation budgets outside supported limits.");
        }
    }
}
