using System;


namespace Emulator6502
{
    /// <summary>
    /// Interface définissant l'accès d'un processeur de la famille 65xx
    /// à l'espace mémoire qui lui est attaché.
    /// <br/>
    /// On rappelle que pour cette famille de processeurs, l'espace-mémoire
    /// inclut aussi (en plus de la mémoire proprement dite) les périphériques
    /// et autres entrées / sorties.
    /// </summary>
    public interface IMemorySpace6502
    {
        /// <summary>
        /// Lit la valeur d'un octet en mémoire (ou entrée de périphérique).
        /// </summary>
        /// <param name="address">Adresse-mémoire de l'octet à lire.</param>
        /// <returns>
        /// La valeur lue à l'adresse donnée.
        /// <br/>
        /// Renvoie <code>null</code> si l'adresse en question n'est pas
        /// accessible en lecture.
        /// </returns>
        Byte? ReadMemory(UInt16 address);

        /// <summary>
        /// Écrit la valeur d'un octet en mémoire (ou sortie de périphérique).
        /// </summary>
        /// <param name="address">Adresse-mémoire de l'octet à écrire.</param>
        /// <param name="value">Valeur de l'octet à écrire.</param>
        void WriteMemory(UInt16 address, Byte value);
    }
}

