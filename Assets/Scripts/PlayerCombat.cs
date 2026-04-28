using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class PlayerCombat : MonoBehaviour
{

    //Ranged attack variables
    [Header("Ranged Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float fireRate = 0.5f;
    private float nextFireTime = 0f;

    //VARIABLES FOR LATER USE
    /*
    //Melee attack variables
    [Header("Mellee Attack")]
    [SerializeField] private Transform weaponPoint;
    [SerializeField] private GameObject hitBox;
    [SerializeField] private float meleeDuration = 0.2f;
    [SerializeField] private float dmgMult = 1f;
    [SerializeField] private float meleeRange = 1f;
    */ 
    //Shield variables
    [Header("Shield")]
    [SerializeField] private GameObject shieldPrefab;
    public static bool isShieldActive = false;
    private GameObject currShield;
    private LineRenderer currLine;
    private EdgeCollider2D currCollider;
    private List<Vector2> shieldPoints;
    private float minPointDistance = 0.1f;
    [SerializeField] private float shieldDuration = 5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ToggleShield(bool isActive)
    {
        isShieldActive = isActive;
        if (isActive)
        {
            // Logic to spawn the Shield Prefab goes here
        }
        else
        {
            // Logic to stop drawing goes here
        }
    }
}
