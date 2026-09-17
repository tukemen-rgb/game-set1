using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists the native-4K shadow/edge policy whenever the benchmark scene is saved.
/// Environment rebuilds already save QualityBlock1990s before capture, so this gate inserts the
/// shadow-stability pass into the existing capture preparation chain without changing Godot or
/// gameplay collision. It validates implementation only; rendered still/temporal evidence remains
/// mandatory for Visual Fidelity scoring.
/// </summary>
[InitializeOnLoad]
public static class QualityBlockShadowStabilitySaveGate
{
    private const string ScenePath = "Assets/Scenes/QualityBlock1990s.unity";
    private static bool applying;

    static QualityBlockShadowStabilitySaveGate()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (applying || !scene.IsValid() || !string.Equals(path, ScenePath, StringComparison.Ordinal))
            return;

        // Only engage once the benchmark has its physical sun and primary quality root. This avoids
        // interfering with incomplete scratch scenes while making the scored scene a persisted invariant.
        if (GameObject.Find("SummerSun") == null || GameObject.Find("QualityBlock1990s") == null)
            return;

        applying = true;
        try
        {
            QualityBlockShadowStabilityUpgrade.ApplyToOpenScene();
            QualityBlockShadowStabilityUpgrade.ValidateOpenScene();
        }
        catch (Exception ex)
        {
            Debug.LogError("Shadow stability save gate FAILED: " + ex.Message);
            throw;
        }
        finally
        {
            applying = false;
        }
    }
}
