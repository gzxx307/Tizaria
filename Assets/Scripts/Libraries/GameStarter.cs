using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameStarter
{
    public static readonly string StartScene = "RootScene";

    // 这个特性确保该方法在游戏启动时，且在任何场景加载前被调用
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    // 启动指定的StartScene
    private static void LoadRootScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (!currentScene.name.Equals(StartScene))
        {
            SceneManager.LoadScene(StartScene);
        }
    }
}