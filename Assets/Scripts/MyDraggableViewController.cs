using System;
using System.Collections;
using GamenChangerCore;
using UnityEngine;
using UnityEngine.UI;

public class MyDraggableViewController : MonoBehaviour, IDraggableCornerHandler
{
    public Vector2[] gridPoints;
    public Vector2[] OnDraggableCornerInitialized(Func<int, int, GamenDriver> getDriver)
    {
        return gridPoints;
    }

    public void OnDragApproachingToGrid(int index, GameObject go)
    {
        OnDragDoneOnGrid(index, go);
    }

    public void OnDragDoneOnGrid(int index, GameObject go)
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
                Debug.LogError("undefined index:" + index);
                break;
        }
    }

    public void OnDragCancelled(int index, GameObject go)
    {
        Debug.Log("index:" + index + " go:" + go);
    }

    public void OnDragApproachAnimationRequired(int index, GameObject go, Vector2 approachTargetPosition, Action onDone, Action onCancelled)
    {
        var rectTrans = go.GetComponent<RectTransform>();
        IEnumerator approach()
        {
            var count = 0;
            while (true)
            {
                rectTrans.anchoredPosition = rectTrans.anchoredPosition + (approachTargetPosition - rectTrans.anchoredPosition) * 0.5f;
                if (count == 10)
                {
                    onDone();
                    yield break;
                }
                count++;
                yield return null;
            }
        }
        StartCoroutine(approach());
    }

    public void OnDragCancelAnimationRequired(GameObject go, Vector2 initialPosition, Action onDone)
    {
        var rectTrans = go.GetComponent<RectTransform>();
        IEnumerator cancel()
        {
            var count = 0;
            while (true)
            {
                rectTrans.anchoredPosition = rectTrans.anchoredPosition + (initialPosition - rectTrans.anchoredPosition) * 0.5f;
                if (count == 10)
                {
                    onDone();
                    yield break;
                }
                count++;
                yield return null;
            }
        }
        StartCoroutine(cancel());
    }
}
