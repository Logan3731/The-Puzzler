using System.Collections;
using UnityEngine;

/// <summary>순간 정지: 짧게 Time.timeScale을 0으로 만들어 "쾅" 하는 무게감을 준다.</summary>
public class HitStop : MonoBehaviour
{
    float until;
    Coroutine routine;

    public void Stop(float duration)
    {
        if (duration <= 0f) return;
        until = Mathf.Max(until, Time.unscaledTime + duration);
        if (routine == null) routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Time.timeScale = 0f;
        while (Time.unscaledTime < until) yield return null;
        Time.timeScale = 1f;
        routine = null;
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        routine = null;
    }
}
