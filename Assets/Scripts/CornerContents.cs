using GamenChangerCore;
using UnityEngine;

public class CornerContents : MonoBehaviour, ICornerContent
{

    public void WillAppear()
    {
        // Debug.Log("WillAppear" + "\tparent:" + transform.parent.gameObject);
    }

    public void DidAppear()
    {
        // Debug.Log("DidAppear" + "\tparent:" + transform.parent.gameObject);
    }

    public void WillDisappear()
    {
        // Debug.Log("WillDisappear" + "\tparent:" + transform.parent.gameObject);
    }

    public void DidDisappear()
    {
        // Debug.Log("DidDisappear" + "\tparent:" + transform.parent.gameObject);
    }

    public void AppearCancelled()
    {
        // Debug.Log("AppearCancelled" + "\tparent:" + transform.parent.gameObject);
    }

    public void DisppearCancelled()
    {
        // Debug.Log("DisppearCancelled" + "\tparent:" + transform.parent.gameObject);
    }

    public void AppearProgress(float progress)
    {
        // Debug.Log("AppearProgress:" + progress + "\tparent:" + transform.parent.gameObject);
    }

    public void DisppearProgress(float progress)
    {
        // Debug.Log("DisppearProgress:" + progress + "\tparent:" + transform.parent.gameObject);
    }
}