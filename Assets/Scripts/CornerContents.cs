using GamenChangerCore;
using UnityEngine;

public class CornerContents : MonoBehaviour, ICornerContent
{

    public void WillAppear()
    {
        Debug.Log("WillAppear");
    }

    public void DidAppear()
    {
        Debug.Log("DidAppear");
    }

    public void WillDisappear()
    {
        Debug.Log("WillDisappear");
    }

    public void DidDisappear()
    {
        Debug.Log("DidDisappear");
    }

    public void AppearProgress(float progress)
    {
        // Debug.Log("AppearProgress:" + progress);
    }

    public void DisppearProgress(float progress)
    {
        // Debug.Log("DisppearProgress:" + progress);
    }

    public void AppearCancelled()
    {
        Debug.Log("AppearCancelled");
    }

    public void DisppearCancelled()
    {
        Debug.Log("DisppearCancelled");
    }
}