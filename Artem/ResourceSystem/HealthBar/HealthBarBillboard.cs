using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthBarBillboard : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        else
        {
            Debug.LogError("Main Camera not found in scene");
        }
    }
    void Update()
    {
        transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
        mainCamera.transform.rotation * Vector3.up);
    }
}
