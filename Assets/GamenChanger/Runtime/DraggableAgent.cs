using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    public class DraggableAgent : MonoBehaviour, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IBeginDragHandler
    {
        public DraggableCorner parent;

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            parent.OnInitializePotentialDrag(this, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            parent.OnBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            parent.OnDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            parent.OnEndDrag(this, eventData);
        }
    }
}