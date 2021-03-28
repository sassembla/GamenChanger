using System;
using UnityEngine;

namespace GamenChangerCore
{
    // TODO: ここでGO以外が扱えると嬉しいなーって思ったりする
    public interface IOneOfNCornerHandler
    {
        void OnInitialized(GameObject one, GameObject[] all, Action<GameObject> setOneOfNAct);
        void OnChangedToOneByPlayer(GameObject one, GameObject old, GameObject[] all);
        void OnChangedToOneByHandler(GameObject one, GameObject old, GameObject[] all);
    }
}