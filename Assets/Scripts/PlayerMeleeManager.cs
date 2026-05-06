using Unity.VisualScripting;
using UnityEngine;

public class PlayerMeleeManager : MonoBehaviour
{
    private enum AttackType {None, Slash, Stab}

    [UnitHeaderInspectable("References")]
    [SerializeField] private Animator anim;
    [SerializeField] private Transform weaponPivot;

    [Header("Tuning")]
    [SerializeField] private float stabTolerance = 2f;
    public bool canAttack = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
