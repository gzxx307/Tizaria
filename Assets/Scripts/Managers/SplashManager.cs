using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SplashManager : MonoBehaviour
{
    [SerializeField] private GameObject Logo;
    private SpriteRenderer _logoSpriteRenderer;

    public float ColorChangeSpeed;
    public float Acceleration;
    public int Distance;
    
    public string Title;
    public float MessageFadeInSpeed;
    public TextMeshProUGUI Message1;
    public TextMeshProUGUI Message2;

    private void Start()
    {
        _logoSpriteRenderer = Logo.GetComponent<SpriteRenderer>();
        
        if (_logoSpriteRenderer != null) StartCoroutine(Splash());
    }

    private IEnumerator Splash()
    {
        // Logo显现
        while (_logoSpriteRenderer.color.a < 1)
        {
            var color = _logoSpriteRenderer.color;
            color.a += ColorChangeSpeed;
            _logoSpriteRenderer.color = color;
            yield return null;
        }

        // Logo上移
        while (Logo.transform.position.y < Distance)
        {
            var vector3 = Logo.transform.position;
            vector3.y += Acceleration * (Logo.transform.position.y - Distance);
            Logo.transform.position = vector3;
            yield return null;
        }
        
        // 打印标题
        
    }

    public IEnumerator MessageFadeIn(TextMeshProUGUI message)
    {
        var color = message.color;
        color.a = 0f;
        message.color = color;

        while (color.a < 1)
        {
            color.a += MessageFadeInSpeed;
            message.color = color;
            
            yield return null;
        }
    }
}