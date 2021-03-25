using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    public interface IOneOfNCornerHandler
    {
        void OnInitialized(GameObject one, GameObject[] all);
        void OnChangedToOne(GameObject one, GameObject[] all);
    }
}