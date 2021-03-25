using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    // TODO: ここでGO以外が扱えると嬉しいなーって思ったりする
    public interface IOneOfNCornerHandler
    {
        void OnInitialized(GameObject one, GameObject[] all);
        void OnChangedToOne(GameObject one, GameObject old, GameObject[] all);
        GameObject OnSelectOneOfNFromCodeWithCorner(Corner choosedCorner, GameObject[] all);
    }
}