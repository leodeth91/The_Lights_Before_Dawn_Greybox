using UnityEngine;

/// <summary>Separa abrir y cerrar para evitar que un intento de abrir termine cerrando un objeto que ya estaba abierto.</summary>
public interface IOpenable
{
    bool IsOpen { get; }
    bool TryOpen(GameObject actor);
    bool TryClose(GameObject actor);
}
