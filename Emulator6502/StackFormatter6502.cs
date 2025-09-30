using System;
using System.Text;

namespace Emulator6502
{
    /// <summary>
    /// Classe mettant en forme le contenu de la pile
    /// d'un processeur de la famille 65x02.
    /// </summary>
    public class StackFormatter6502
    {
        /* =========================== CONSTANTES =========================== */

        // messages affichés
        private const String ERR_UNREADABLE_ADDRESS =
                "Impossible de lire le contenu de l'adresse ${0:X4} !";


        /* ========================== CHAMPS PRIVÉS ========================= */

        // espace-mémoire attaché au processeur
        // (défini une fois pour toutes à la construction)
        private readonly IMemorySpace6502 memSpace;


        /* ========================== CONSTRUCTEUR ========================== */

        /// <summary>
        /// Constructeur de référence (et unique) de la classe.
        /// </summary>
        /// <param name="memorySpace">
        /// Espace-mémoire où lire les valeurs à présenter.
        /// </param>
        public StackFormatter6502(IMemorySpace6502 memorySpace)
        {
            this.memSpace = memorySpace;
        }


        /* ======================== MÉTHODES PRIVÉES ======================== */

        /* ~~~~ accès à l'espace mémoire ~~~~ */

        private byte ReadMem(ushort addr)
        {
            byte? memval = this.memSpace.ReadMemory(addr);
            if (!(memval.HasValue)) {
                throw new AddressUnreadableException(
                        addr,
                        String.Format(ERR_UNREADABLE_ADDRESS,
                                      addr));
            }
            return memval.Value;
        }

        /* ======================= MÉTHODES PUBLIQUES ======================= */

        /// <summary>
        /// Liste les valeurs empilées dans la mémoire du proceseur.
        /// </summary>
        /// <param name="regSvalue">
        /// Valeur du registre S (donne l'étendue courante de la pile).
        /// </param>
        /// <returns>
        /// Les valeurs présentes dans la pile du processeur sous forme texte,
        /// formatées de façon adéquate.
        /// </returns>
        /// <exception cref="AddressUnreadableException">
        /// Si l'une des adresses-mémoire à traiter est impossible à lire.
        /// </exception>
        public String ListStackValues(byte regSvalue)
        {
            StringBuilder sbResult = new StringBuilder();

            /* affiche les valeurs dans l'ordre d'empilage */
            ushort sp = (ushort)(0x0100 + regSvalue);
            for (ushort addr = 0x01ff; addr > sp; addr--) {
                byte val = ReadMem(addr);
                sbResult.Append(String.Format("{0:X4} : {1:X2}\r\n",
                                addr, val));
            }

            /* terminé */
            return sbResult.ToString();
        }


        /* ====================== PROPRIÉTÉS PUBLIQUES ====================== */

        /// <summary>
        /// Objet espace-mémoire attaché au lors de sa création.
        /// (Propriété en lecture seule.)
        /// </summary>
        public IMemorySpace6502 MemorySpace
        {
            get { return this.memSpace; }
        }

    }
}

