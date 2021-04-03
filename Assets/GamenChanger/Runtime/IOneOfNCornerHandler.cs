using System;
using UnityEngine;

namespace GamenChangerCore
{
    // TODO: ここでGO以外が扱えると嬉しいなーって思ったりする
    public interface IOneOfNCornerHandler
    {
        void OnOneOfNCornerReloaded(GameObject one, GameObject[] all, Action<GameObject> setOneOfNAct);
        void OnOneOfNChangedToOneByPlayer(GameObject one, GameObject before, GameObject[] all);
        void OnOneOfNChangedToOneByHandler(GameObject one, GameObject before, GameObject[] all);
    }
}