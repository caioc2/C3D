using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSCounter : MonoBehaviour {
    float deltaTime = 0.0f;

    bool start = false;
    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        msec = deltaTime * 1000.0f;
        fps = 1.0f / deltaTime;
        if (fps > 59.0f) start = true;

        if(start)
            min = Mathf.Min(min, fps);
    }

    float min=10000.0f;
    float msec;
    float fps;
    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        GUIStyle style = new GUIStyle();

        Rect rect = new Rect(0, 0, w, h * 2 / 50);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = h * 2 / 50;
        style.normal.textColor = new Color(1.0f, 0.5f, 0.0f, 1.0f);
        
        string text = string.Format("{0:0.0} ms ({1:0.0} fps), ({2:0.0} min fps)", msec, fps, min);
        GUI.Label(rect, text, style);
    }
}
