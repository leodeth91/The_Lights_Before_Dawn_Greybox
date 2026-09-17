using System.Collections.Generic;
using UnityEngine;

/// <summary>Entrar al trigger habilita E; salir lo retira. Varios colliders del Player cuentan como un solo personaje.</summary>
public sealed class StoryPhoneProximitySensor : MonoBehaviour
{
    private StoryPhoneInteractionArea area;
    private PlayerInteractor nearbyPlayer;
    private readonly HashSet<Collider> contacts = new HashSet<Collider>();
    public void Configure(StoryPhoneInteractionArea interactionArea) { area = interactionArea; }
    private void OnTriggerEnter(Collider other) { Register(other); }
    private void OnTriggerStay(Collider other) { Register(other); }
    private void Register(Collider other)
    {
        PlayerInteractor player = other.GetComponentInParent<PlayerInteractor>();
        if (player == null || area == null) return;
        contacts.Add(other); nearbyPlayer = player;
        player.SetNearbyPhone(area);
    }
    private void OnTriggerExit(Collider other)
    {
        if (!contacts.Remove(other) || contacts.Count > 0) return;
        Clear();
    }
    private void OnDisable() { Clear(); }
    private void Clear()
    {
        if (nearbyPlayer != null) nearbyPlayer.ClearNearbyPhone(area);
        nearbyPlayer = null; contacts.Clear();
    }
}
