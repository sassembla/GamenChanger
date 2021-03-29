using GamenChangerCore;
using UnityEngine;
using UnityEngine.UI;

public class MyDraggableViewController : MonoBehaviour, IDraggableCornerHandler
{
    public Vector2[] gridPoints;
    public Vector2[] OnInitialized()
    {
        return gridPoints;
    }

    public void OnDragApproachingToGrid(int index, GameObject go)
    {
        OnGrid(index, go);
    }

    public void OnGrid(int index, GameObject go)
    {
        var text = go.GetComponentInChildren<Text>();
        switch (index)
        {
            case 0:
                text.text = "yes";
                break;
            case 1:
                text.text = "y or n";
                break;
            case 2:
                text.text = "no";
                break;
            default:
                Debug.LogError("");
                break;
        }
    }
}
