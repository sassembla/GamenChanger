using UnityEngine;

namespace GamenChangerCore
{
    public interface IOneOfNCornerHandler
    {
        void OnInitialized(GameObject one);
        void OnChangedToOne(GameObject one);
    }
}