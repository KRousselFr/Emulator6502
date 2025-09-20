using System;


namespace Emulator6502
{
    /// <summary>
    /// Exception lancée lorsqu'un opcode invalide est rencontré à l'exécution.
    /// </summary>
    class UnknownOpcodeException : Exception
    {
        /* ========================= CHAMPS PRIVÉS ========================== */

        private readonly ushort addr;
        private readonly byte code;

        /* ========================= CONSTRUCTEURS ========================== */

        public UnknownOpcodeException(UInt16 address, Byte opcode) : base()
        {
            this.addr = address;
            this.code = opcode;
        }

        public UnknownOpcodeException(UInt16 address, byte opcode, string message) : base(message)
        {
            this.addr = address;
            this.code = opcode;
        }

        /* ====================== PROPRIÉTÉS PUBLIQUES ====================== */

        /// <summary>
        /// Adresse-mémoire où l'opcode invalide a été lu.
        /// (Propriété en lecture seule.)
        /// </summary>
        public UInt16 MemoryAddress
        {
            get { return this.addr; }
        }

        /// <summary>
        /// Opcode invalide lu en mémoire.
        /// (Propriété en lecture seule.)
        /// </summary>
        public Byte Opcode
        {
            get { return this.code; }
        }

    }
}


