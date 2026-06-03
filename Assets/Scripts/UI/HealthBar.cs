using UnityEngine;
using UnityEngine.UI; // Crucial for accessing the Image component

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image healthBarFill;

    // Call this function whenever the player takes damage or heals
    public void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        // Dividing current by max gives us a percentage between 0.0 and 1.0
        healthBarFill.fillAmount = currentHealth / maxHealth;
    }
}