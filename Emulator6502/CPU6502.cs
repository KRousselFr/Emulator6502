using System;


namespace Emulator6502
{
    /// <summary>
    /// Classe émulant un processeur 6502 originel (dans la version
    /// de 1977, de technologie NMOS).
    /// </summary>
    public class CPU6502
    {
        /* =========================== CONSTANTES =========================== */

        // messages afffichés
        private const String ERR_UNREADABLE_ADDRESS =
                "Impossible de lire le contenu de l'adresse ${0:X4} !";
        private const String ERR_UNKNOWN_OPCODE =
                "Opcode invalide (${1:X2}) rencontré à l'adresse ${0:X4} !";

        // valeur binaire des "flags" dans le registre P
        const byte FLAG_C = 0x01;
        const byte FLAG_Z = 0x02;
        const byte FLAG_I = 0x04;
        const byte FLAG_D = 0x08;
        const byte FLAG_B = 0x10;
        const byte FLAG_V = 0x40;
        const byte FLAG_N = 0x80;

        // adresses particulières
        const ushort PAGE_0_BASE = 0x0000;
        const ushort PAGE_1_BASE = 0x0100;
        const ushort NMI_VECTOR = 0xFFFA;
        const ushort RESET_VECTOR = 0xFFFC;
        const ushort BRK_IRQ_VECTOR = 0xFFFE;

        // masques de sélection de bit
        const byte BYTE_MSB_MASK = 0x80;
        const byte BYTE_LSB_MASK = 0x01;


        /* ========================== CHAMPS PRIVÉS ========================= */

        // espace-mémoire attaché au processeur
        // (défini une fois pour toutes à la construction)
        private readonly IMemorySpace6502 memSpace;

        // registres du processeur
        private byte regA;
        private byte regX;
        private byte regY;
        private byte regS;
        private ushort regPC;
        // "flags" composant le "registre P" (état du processeur)
        private bool flagC;
        private bool flagZ;
        private bool flagI;
        private bool flagD;
        private bool flagB;
        private bool flagV;
        private bool flagN;

        // comptage des cycles écoulés
        private ulong cycles;

        // lignes de requêtes d'interruption
        private bool resetLine;
        private bool nmiLine;
        private bool nmiTrig;   // "flag" interne de déclenchement de NMI
        private bool irqLine;

        // politique vis-à-vis des opcodes invalides
        private UnknownOpcodePolicy uoPolicy;


        /* ========================== CONSTRUCTEUR ========================== */

        /// <summary>
        /// Contructeur de référence (et unique) de la classe CPU6502.
        /// </summary>
        /// <param name="memorySpace">
        /// Espace-mémoire à attacher à ce nouveau processeur.
        /// </param>
        public CPU6502(IMemorySpace6502 memorySpace)
        {
            this.memSpace = memorySpace;
            this.cycles = 0L;
            this.resetLine = false;
            this.nmiLine = this.nmiTrig = false;
            this.irqLine = false;
            this.uoPolicy = UnknownOpcodePolicy.ThrowException;
//            Reset();
        }


        /* ======================== MÉTHODES PRIVÉES ======================== */

        private byte HiByte(ushort word)
        {
            return (byte)((word >> 8) & 0x00ff);
        }

        private byte LoByte(ushort word)
        {
            return (byte)(word & 0x00ff);
        }

        private ushort MakeWord(byte hi, byte lo)
        {
            return (ushort)((hi << 8) | lo);
        }

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
            this.cycles++;
            return memval.Value;
        }

        private void WriteMem(ushort addr, byte val)
        {
            this.memSpace.WriteMemory(addr, val);
            this.cycles++;
        }

        /* ~~~~ gestion des "flags" ~~~~ */

        private void SetNZ(byte val)
        {
            this.flagZ = (val == 0);
            this.flagN = ((val & BYTE_MSB_MASK) != 0);
        }

        /* ~~~~ implantation des modes d'adressage ~~~~ */

        /* mode d'adressage immédiat : INSTR #nn  */
        private byte AddrModeImmediateValue()
        {
            return ReadMem(this.regPC);
        }

        /* mode d'adressage absolu : INSTR $xxxx  */
        private ushort AddrModeAbsoluteAddress()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            ushort addr = MakeWord(hi, lo);
            return addr;
        }
        private byte AddrModeAbsoluteValue()
        {
            return ReadMem(AddrModeAbsoluteAddress());
        }

        /* mode d'adressage absolu indexé sur X : INSTR $xxxx, X  */
        private ushort AddrModeAbsoluteIndexedXAddress()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            ushort addr = MakeWord(hi, lo);
            ushort addr2 = (ushort)(addr + this.regX);
            if (HiByte(addr2) != HiByte(addr)) {
                this.cycles++;
            }
            return addr2;
        }
        private byte AddrModeAbsoluteIndexedXValue()
        {
            return ReadMem(AddrModeAbsoluteIndexedXAddress());
        }

        /* mode d'adressage absolu indexé sur Y : INSTR $xxxx, Y  */
        private ushort AddrModeAbsoluteIndexedYAddress()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            ushort addr = MakeWord(hi, lo);
            ushort addr2 = (ushort)(addr + this.regY);
            if (HiByte(addr2) != HiByte(addr)) {
                this.cycles++;
            }
            return addr2;
        }
        private byte AddrModeAbsoluteIndexedYValue()
        {
            return ReadMem(AddrModeAbsoluteIndexedYAddress());
        }

        /* mode d'adressage absolu indirect : INSTR ($xxxx)  */
        private ushort AddrModeAbsoluteIndirectAddress()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            ushort addr = MakeWord(hi, lo);
            lo = ReadMem(addr);
            if (LoByte(addr) == 0xff) addr -= 0x0100; // BUG du 6502 d'origine !
            addr++;
            hi = ReadMem(addr);
            ushort addr2 = MakeWord(hi, lo);
            return addr2;
        }
        private byte AddrModeAbsoluteIndirectValue()
        {
            return ReadMem(AddrModeAbsoluteIndirectAddress());
        }

        /* mode d'adressage page-zéro : INSTR $xx  */
        private ushort AddrModeZeroPageAddress()
        {
            byte lo = ReadMem(this.regPC);
            return MakeWord(0x00, lo);
        }
        private byte AddrModeZeroPageValue()
        {
            return ReadMem(AddrModeZeroPageAddress());
        }

        /* mode d'adressage page-zéro indexé sur X : INSTR $xx, X  */
        private ushort AddrModeZeroPageIndexedXAddress()
        {
            byte lo = ReadMem(this.regPC);
            lo += this.regX;
            ushort addr = MakeWord(0x00, lo);
            return addr;
        }
        private byte AddrModeZeroPageIndexedXValue()
        {
            return ReadMem(AddrModeZeroPageIndexedXAddress());
        }

        /* mode d'adressage page-zéro indexé sur Y : INSTR $xx, Y  */
        private ushort AddrModeZeroPageIndexedYAddress()
        {
            byte lo = ReadMem(this.regPC);
            lo += this.regY;
            ushort addr = MakeWord(0x00, lo);
            return addr;
        }
        private byte AddrModeZeroPageIndexedYValue()
        {
            return ReadMem(AddrModeZeroPageIndexedYAddress());
        }

        /* mode d'adressage page-zéro indexé sur X indirect : INSTR ($xx, X)  */
        private ushort AddrModeZeroPageIndexedXIndirectAddress()
        {
            byte lo = ReadMem(this.regPC);
            lo += this.regX;
            ushort indAddr = MakeWord(0x00, lo);
            lo = ReadMem(indAddr);
            indAddr++;
            byte hi = ReadMem(indAddr);
            ushort addr = MakeWord(hi, lo);
            return addr;
            ;
        }
        private byte AddrModeZeroPageIndexedXIndirectValue()
        {
            return ReadMem(AddrModeZeroPageIndexedXIndirectAddress());
        }

        /* mode d'adressage page-zéro indirect indexé sur Y : INSTR ($xx), Y  */
        private ushort AddrModeZeroPageIndirectIndexedYAddress()
        {
            byte lo = ReadMem(this.regPC);
            ushort indAddr = MakeWord(0x00, lo);
            lo = ReadMem(indAddr);
            indAddr++;
            byte hi = ReadMem(indAddr);
            ushort addr = MakeWord(hi, lo);
            ushort addr2 = (ushort)(addr + this.regY);
            if (HiByte(addr2) != HiByte(addr)) {
                this.cycles++;
            }
            return addr2;
        }
        private byte AddrModeZeroPageIndirectIndexedYValue()
        {
            return ReadMem(AddrModeZeroPageIndirectIndexedYAddress());
        }

        /* mode d'adressage relatif : Bxx ±nnn  */
        private ushort AddrModeRelativeAddress()
        {
            sbyte dpl = (sbyte)(ReadMem(this.regPC));
            this.regPC++;
            ushort addr = (ushort)(this.regPC + dpl);
            if (HiByte(addr) != HiByte(this.regPC)) {
                this.cycles++;
            }
            return addr;
        }

        /* mode d'adressage lié à la pile : PHx, PLx...  */
        private ushort AddrModeStackAddress()
        {
            return (ushort)(PAGE_1_BASE | this.regS);
        }

        /* ~~~~ accès à la pile ~~~~ */

        private void PushByte(byte val)
        {
            ushort addr = AddrModeStackAddress();
            WriteMem(addr, val);
            this.regS--;
        }

        private void PushWord(ushort val)
        {
            PushByte(HiByte(val));
            PushByte(LoByte(val));
        }

        private byte PullByte()
        {
            ushort addr = AddrModeStackAddress();
            byte val = ReadMem(addr);
            this.regS++;
            return val;
        }

        private ushort PullWord()
        {
            byte lo = PullByte();
            byte hi = PullByte();
            return MakeWord(hi, lo);
        }

        /* ~~~~ implantation des instructions ~~~~ */

        private void InstrADC(byte val)
        {
            /* signe des opérandes */
            bool n1 = ((this.regA & BYTE_MSB_MASK) != 0);
            bool n2 = ((val & BYTE_MSB_MASK) != 0);
            /* addition proprement dite */
            int res = this.regA + val;
            if (this.flagC) res++;
            this.regA = (byte)res;
            /* BUG du 6502 d'origine : les flags N et Z ne tiennent pas
             * compte des éventuelles corrections liées au mode décimal ! */
            SetNZ(this.regA);
            /*
             * V est activé :
             * - si la somme de deux positifs donne un négatif, ou :
             * - si la somme de deux négatifs donne un positif
             */
            this.flagV = (!n1 & !n2 & this.flagN) |
                         (n1 & n2 & !(this.flagN));
            /* la valeur de C dépend du mode */
            if (this.flagD) {
                /* gestion du mode décimal :
                   correction de la somme si besoin (DAA) */
                if ((this.regA & 0x0f) > 0x09) {
                    this.regA += 0x06;
                }
                if (this.regA > 0x99) {
                    this.regA += 0x60;
                    this.flagC = true;
                }
            } else {
                this.flagC = (res > 0xff);
            }
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrAND(byte val)
        {
            this.regA = (byte)(val & this.regA);
            SetNZ(this.regA);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private byte InstrASL(byte val)
        {
            // instruction agissant sur l'accumulateur ou en mémoire !
            this.flagC = ((val & BYTE_MSB_MASK) != 0);
            val = (byte)(val << 1);
            SetNZ(val);
            this.cycles++;
            return val;
        }

        private void InstrBCC(ushort addr)
        {
            if (!(this.flagC)) {
                this.regPC = addr;
                this.cycles++;
            }
        }

        private void InstrBCS(ushort addr)
        {
            if (this.flagC) {
                this.regPC = addr;
                this.cycles++;
            }
        }

        private void InstrBEQ(ushort addr)
        {
            if (this.flagZ) {
                this.regPC = addr;
                this.cycles++;
            }
        }

        private void InstrBIT(byte val)
        {
            this.flagN = ((val & BYTE_MSB_MASK) != 0);
            this.flagV = ((val & 0x40) != 0);
            byte res = (byte)(val & this.regA);
            this.flagZ = (val == 0);
            this.cycles++;
        }

        private void InstrBMI(ushort addr)
        {
            if (this.flagN) {
                this.regPC = addr;
                this.cycles++;
            }
        }

        private void InstrBNE(ushort addr)
        {
            if (!(this.flagZ)) {
                this.regPC = addr;
                this.cycles++;
            }
        }

        private void InstrBPL(ushort addr)
        {
            if (!(this.flagN)) {
                this.regPC = addr;
                this.cycles++;
            }
        }

        private void InstrBRK()
        {
            this.cycles++;
            // incrémente de registre PC
            this.regPC++;
            // enregistre le contexte actuel
            PushWord(this.regPC);
            this.flagB = true;   // instruction BRK
            PushByte(this.RegisterP);
            // désactive toute autre interruption masquable
            this.flagI = true;
            // lecture du vecteur BRK/IRQ
            byte lo = ReadMem(BRK_IRQ_VECTOR);
            byte hi = ReadMem(BRK_IRQ_VECTOR + 1);
            // saute au vecteur ainsi lu
            this.regPC = MakeWord(hi, lo);
        }

        private void InstrBVC(ushort addr)
        {
            if (!(this.flagV)) {
                this.regPC = addr;
                this.cycles++;
            }
        }

        private void InstrBVS(ushort addr)
        {
            if (this.flagV) {
                this.regPC = addr;
                this.cycles++;
            }
        }

        private void InstrCLC()
        {
            this.flagC = false;
            this.cycles++;
        }

        private void InstrCLD()
        {
            this.flagD = false;
            this.cycles++;
        }

        private void InstrCLI()
        {
            this.flagI = false;
            this.cycles++;
        }

        private void InstrCLV()
        {
            this.flagV = false;
            this.cycles++;
        }

        private void InstrCMP(byte val)
        {
            byte res = (byte)(this.regA - val);
            SetNZ(res);
            this.flagC = (this.regA >= val);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrCPX(byte val)
        {
            byte res = (byte)(this.regX - val);
            SetNZ(res);
            this.flagC = (this.regX >= val);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrCPY(byte val)
        {
            byte res = (byte)(this.regY - val);
            SetNZ(res);
            this.flagC = (this.regY >= val);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrDEC(ushort addr)
        {
            byte val = ReadMem(addr);
            val--;
            SetNZ(val);
            WriteMem(addr, val);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrDEX()
        {
            this.regX--;
            SetNZ(this.regX);
            this.cycles++;
        }

        private void InstrDEY()
        {
            this.regY--;
            SetNZ(this.regY);
            this.cycles++;
        }

        private void InstrEOR(byte val)
        {
            this.regA = (byte)(val ^ this.regA);
            SetNZ(this.regA);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrINC(ushort addr)
        {
            byte val = ReadMem(addr);
            val++;
            SetNZ(val);
            WriteMem(addr, val);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrINX()
        {
            this.regX++;
            SetNZ(this.regX);
            this.cycles++;
        }

        private void InstrINY()
        {
            this.regY++;
            SetNZ(this.regY);
            this.cycles++;
        }

        private void InstrJMP(ushort addr)
        {
            this.regPC = addr;
            this.cycles++;
        }

        private void InstrJSR(ushort addr)
        {
            PushWord(this.regPC);
            this.regPC = addr;
            this.cycles++;
        }

        private void InstrLDA(byte val)
        {
            this.regA = val;
            SetNZ(this.regA);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrLDX(byte val)
        {
            this.regX = val;
            SetNZ(this.regX);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrLDY(byte val)
        {
            this.regY = val;
            SetNZ(this.regY);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private byte InstrLSR(byte val)
        {
            // instruction agissant sur l'accumulateur ou en mémoire !
            this.flagC = ((val & BYTE_LSB_MASK) != 0);
            val = (byte)(val >> 1);
            SetNZ(val);
            this.cycles++;
            return val;
        }

        private void InstrNOP()
        {
            this.cycles++;
            // rien d'autre à faire.
        }

        private void InstrORA(byte val)
        {
            this.regA = (byte)(val | this.regA);
            SetNZ(this.regA);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrPHA()
        {
            PushByte(this.regA);
            this.cycles++;
        }

        private void InstrPHP()
        {
            PushByte(this.RegisterP);
            this.cycles++;
        }

        private void InstrPLA()
        {
            this.regA = PullByte();
            this.cycles++;
            SetNZ(this.regA);
            this.cycles++;
        }

        private void InstrPLP()
        {
            this.RegisterP = PullByte();
            this.cycles++;
            this.cycles++;
        }

        private byte InstrROL(byte val)
        {
            // instruction agissant sur l'accumulateur ou en mémoire !
            bool lsb = this.flagC;
            this.flagC = ((val & BYTE_MSB_MASK) != 0);
            val = (byte)(val << 1);
            if (lsb) val |= BYTE_LSB_MASK;
            SetNZ(val);
            this.cycles++;
            return val;
        }

        private byte InstrROR(byte val)
        {
            // instruction agissant sur l'accumulateur ou en mémoire !
            bool msb = this.flagC;
            this.flagC = ((val & BYTE_LSB_MASK) != 0);
            val = (byte)(val >> 1);
            if (msb) val |= BYTE_MSB_MASK;
            SetNZ(val);
            this.cycles++;
            return val;
        }

        private void InstrRTI()
        {
            this.RegisterP = PullByte();
            this.regPC = PullWord();
            this.cycles += 2;
        }

        private void InstrRTS()
        {
            this.regPC = PullWord();
            this.regPC++;
            this.cycles += 3;
        }

        private void InstrSBC(byte val)
        {
            /* signe des opérandes */
            bool n1 = ((this.regA & BYTE_MSB_MASK) != 0);
            bool n2 = ((val & BYTE_MSB_MASK) != 0);
            /* soustraction proprement dite */
            int res = this.regA - val;
            if (!(this.flagC)) res--;
            this.regA = (byte)res;
            /* BUG du 6502 d'origine : les flags N et Z ne tiennent pas
             * compte des éventuelles corrections liées au mode décimal ! */
            SetNZ(this.regA);
            /*
             * V est activé :
             * - si un positif moins un négatif donne un négatif, ou :
             * - si un négatif moins un positif donne un positif
             */
            this.flagV = (!n1 & n2 & this.flagN) |
                         (n1 & !n2 & !(this.flagN));
            /* la valeur de C dépend du mode */
            if (this.flagD) {
                /* gestion du mode décimal :
                   correction de la différence si besoin (DAS) */
                this.flagC = true;
                if ((this.regA & 0x0f) > 0x09) {
                    this.regA -= 0x06;
                }
                if (this.regA > 0x99) {
                    this.regA -= 0x60;
                    this.flagC = false;
                }
            } else {
                this.flagC = (res >= 0);
            }
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrSEC()
        {
            this.flagC = true;
            this.cycles++;
        }

        private void InstrSED()
        {
            this.flagD = true;
            this.cycles++;
        }

        private void InstrSEI()
        {
            this.flagI = true;
            this.cycles++;
        }

        private void InstrSTA(ushort addr)
        {
            WriteMem(addr, this.regA);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrSTX(ushort addr)
        {
            WriteMem(addr, this.regX);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrSTY(ushort addr)
        {
            WriteMem(addr, this.regY);
            this.cycles++;
            // TODO vérifier les cycles !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        private void InstrTAX()
        {
            this.regX = this.regA;
            SetNZ(this.regX);
            this.cycles++;
        }

        private void InstrTAY()
        {
            this.regY = this.regA;
            SetNZ(this.regY);
            this.cycles++;
        }

        private void InstrTSX()
        {
            this.regX = this.regS;
            SetNZ(this.regX);
            this.cycles++;
        }

        private void InstrTXA()
        {
            this.regA = this.regX;
            SetNZ(this.regA);
            this.cycles++;
        }

        private void InstrTXS()
        {
            this.regS = this.regX;
            // PAS de mise à jour des flags pour TXS !
            this.cycles++;
        }

        private void InstrTYA()
        {
            this.regA = this.regY;
            SetNZ(this.regA);
            this.cycles++;
        }


        /* ======================= MÉTHODES PUBLIQUES ======================= */

        /// <summary>
        /// Réinitialise le processeur.
        /// </summary>
        /// <exception cref="AddressUnreadableException">
        /// Si une adresse-mémoire (vecteur RESET ou sa cible)
        /// ne peut pas être lue.
        /// </exception>
        public void Reset()
        {
            // initialisation interne des circuits
            this.cycles += 5;
            // désactive toute interruption masquable
            this.flagI = true;
            // lecture du vecteur RESET
            byte lo = ReadMem(RESET_VECTOR);
            byte hi = ReadMem(RESET_VECTOR + 1);
            // saute au vecteur ainsi lu
            this.regPC = MakeWord(hi, lo);
        }

        /// <summary>
        /// Lance une interruption matérielle non-masquable (NMI).
        /// </summary>
        /// <exception cref="AddressUnreadableException">
        /// Si une adresse-mémoire (vecteur NMI ou sa cible)
        /// ne peut pas être lue.
        /// </exception>
        public void TriggerNMI()
        {
            this.nmiTrig = true;
        }

        /// <summary>
        /// Exécute l'instruction actuellement pointée par le registre PC.
        /// </summary>
        /// <returns>
        /// Nombre de cycles écoulés pour l'exécution de l'instruction.
        /// </returns>
        /// <exception cref="AddressUnreadableException">
        /// Si le contenu d'une adresse-mémoire nécessaire au travail
        /// du processeur ne peut pas être lu.
        /// </exception>
        public ulong Step()
        {
            ulong cycBegin = this.cycles;

            // la ligne reset empêche le processeur de travailler
            if (this.resetLine) return 0L;

            // une interruption est-elle signalée ?
            if (this.nmiTrig) {
                // NMI : sensible à la transition
                this.nmiTrig = false;
                // lance la réponse à l'interruption
                this.cycles += 2;
                // enregistre le contexte actuel
                PushWord(this.regPC);
                this.flagB = false;   // pas une instruction BRK
                PushWord(this.RegisterP);
                // désactive toute interruption masquable
                this.flagI = true;
                // lecture du vecteur IRQ/BRK
                byte lo = ReadMem(NMI_VECTOR);
                byte hi = ReadMem(NMI_VECTOR + 1);
                // saute au vecteur ainsi lu
                this.regPC = MakeWord(hi, lo);
            } else if (this.irqLine) {
                // interruption masquable
                if (!(this.flagI)) {
                    // lance la réponse à l'interruption
                    this.cycles += 2;
                    // enregistre le contexte actuel
                    PushWord(this.regPC);
                    this.flagB = false;   // pas une instruction BRK
                    PushWord(this.RegisterP);
                    // désactive toute autre interruption masquable
                    this.flagI = true;
                    // lecture du vecteur IRQ/BRK
                    byte lo = ReadMem(BRK_IRQ_VECTOR);
                    byte hi = ReadMem(BRK_IRQ_VECTOR + 1);
                    // saute au vecteur ainsi lu
                    this.regPC = MakeWord(hi, lo);
                }
            }

            // lit, décode et exécute le prochain opcode
            byte opcode = ReadMem(this.regPC);
            this.cycles++;
            this.regPC++;

            ushort addr;
            byte val;
            switch (opcode) {
                case 0x00:
                    InstrBRK();
                    break;
                case 0x01:
                    val = AddrModeZeroPageIndexedXIndirectValue();
                    InstrORA(val);
                    break;
                case 0x05:
                    val = AddrModeZeroPageValue();
                    InstrORA(val);
                    break;
                case 0x06:
                    addr = AddrModeZeroPageAddress();
                    val = ReadMem(addr);
                    val = InstrASL(val);
                    WriteMem(addr, val);
                    break;
                case 0x08:
                    InstrPHP();
                    break;
                case 0x09:
                    val = AddrModeImmediateValue();
                    InstrORA(val);
                    break;
                case 0x0a:
                    val = this.regA;
                    val = InstrASL(val);
                    this.regA = val;
                    break;
                case 0x0d:
                    val = AddrModeAbsoluteValue();
                    InstrORA(val);
                    break;
                case 0x0e:
                    addr = AddrModeAbsoluteAddress();
                    val = ReadMem(addr);
                    val = InstrASL(val);
                    WriteMem(addr, val);
                    break;

                case 0x10:
                    addr = AddrModeRelativeAddress();
                    InstrBPL(addr);
                    break;
                case 0x11:
                    val = AddrModeZeroPageIndirectIndexedYValue();
                    InstrORA(val);
                    break;
                case 0x15:
                    val = AddrModeZeroPageIndexedXValue();
                    InstrORA(val);
                    break;
                case 0x16:
                    addr = AddrModeZeroPageIndexedXAddress();
                    val = ReadMem(addr);
                    val = InstrASL(val);
                    WriteMem(addr, val);
                    break;
                case 0x18:
                    InstrCLC();
                    break;
                case 0x19:
                    val = AddrModeAbsoluteIndexedYValue();
                    InstrORA(val);
                    break;
                case 0x1d:
                    val = AddrModeAbsoluteIndexedXValue();
                    InstrORA(val);
                    break;
                case 0x1e:
                    addr = AddrModeAbsoluteIndexedXAddress();
                    val = ReadMem(addr);
                    val = InstrASL(val);
                    WriteMem(addr, val);
                    break;

                case 0x20:
                    addr = AddrModeAbsoluteAddress();
                    InstrJSR(addr);
                    break;
                case 0x21:
                    val = AddrModeZeroPageIndexedXIndirectValue();
                    InstrAND(val);
                    break;
                case 0x24:
                    val = AddrModeZeroPageValue();
                    InstrBIT(val);
                    break;
                case 0x25:
                    val = AddrModeZeroPageValue();
                    InstrAND(val);
                    break;
                case 0x26:
                    addr = AddrModeZeroPageAddress();
                    val = ReadMem(addr);
                    val = InstrROL(val);
                    WriteMem(addr, val);
                    break;
                case 0x28:
                    InstrPLP();
                    break;
                case 0x29:
                    val = AddrModeImmediateValue();
                    InstrAND(val);
                    break;
                case 0x2a:
                    val = this.regA;
                    val = InstrROL(val);
                    this.regA = val;
                    break;
                case 0x2c:
                    val = AddrModeAbsoluteValue();
                    InstrBIT(val);
                    break;
                case 0x2d:
                    val = AddrModeAbsoluteValue();
                    InstrAND(val);
                    break;
                case 0x2e:
                    addr = AddrModeAbsoluteAddress();
                    val = ReadMem(addr);
                    val = InstrROL(val);
                    WriteMem(addr, val);
                    break;

                case 0x30:
                    addr = AddrModeRelativeAddress();
                    InstrBMI(addr);
                    break;
                case 0x31:
                    val = AddrModeZeroPageIndirectIndexedYValue();
                    InstrAND(val);
                    break;
                case 0x35:
                    val = AddrModeZeroPageIndexedXValue();
                    InstrAND(val);
                    break;
                case 0x36:
                    addr = AddrModeZeroPageIndexedXAddress();
                    val = ReadMem(addr);
                    val = InstrROL(val);
                    WriteMem(addr, val);
                    break;
                case 0x38:
                    InstrSEC();
                    break;
                case 0x39:
                    val = AddrModeAbsoluteIndexedYValue();
                    InstrAND(val);
                    break;
                case 0x3d:
                    val = AddrModeAbsoluteIndexedXValue();
                    InstrAND(val);
                    break;
                case 0x3e:
                    addr = AddrModeAbsoluteIndexedXAddress();
                    val = ReadMem(addr);
                    val = InstrROL(val);
                    WriteMem(addr, val);
                    break;

                case 0x40:
                    InstrRTI();
                    break;
                case 0x41:
                    val = AddrModeZeroPageIndexedXIndirectValue();
                    InstrEOR(val);
                    break;
                case 0x45:
                    val = AddrModeZeroPageValue();
                    InstrEOR(val);
                    break;
                case 0x46:
                    addr = AddrModeZeroPageAddress();
                    val = ReadMem(addr);
                    val = InstrLSR(val);
                    WriteMem(addr, val);
                    break;
                case 0x48:
                    InstrPHA();
                    break;
                case 0x49:
                    val = AddrModeImmediateValue();
                    InstrEOR(val);
                    break;
                case 0x4a:
                    val = this.regA;
                    val = InstrLSR(val);
                    this.regA = val;
                    break;
                case 0x4c:
                    addr = AddrModeAbsoluteAddress();
                    InstrJMP(addr);
                    break;
                case 0x4d:
                    val = AddrModeAbsoluteValue();
                    InstrEOR(val);
                    break;
                case 0x4e:
                    addr = AddrModeAbsoluteAddress();
                    val = ReadMem(addr);
                    val = InstrLSR(val);
                    WriteMem(addr, val);
                    break;

                case 0x50:
                    addr = AddrModeRelativeAddress();
                    InstrBVC(addr);
                    break;
                case 0x51:
                    val = AddrModeZeroPageIndirectIndexedYValue();
                    InstrEOR(val);
                    break;
                case 0x55:
                    val = AddrModeZeroPageIndexedXValue();
                    InstrEOR(val);
                    break;
                case 0x56:
                    addr = AddrModeZeroPageIndexedXAddress();
                    val = ReadMem(addr);
                    val = InstrLSR(val);
                    WriteMem(addr, val);
                    break;
                case 0x58:
                    InstrCLI();
                    break;
                case 0x59:
                    val = AddrModeAbsoluteIndexedYValue();
                    InstrEOR(val);
                    break;
                case 0x5d:
                    val = AddrModeAbsoluteIndexedXValue();
                    InstrEOR(val);
                    break;
                case 0x5e:
                    addr = AddrModeAbsoluteIndexedXAddress();
                    val = ReadMem(addr);
                    val = InstrLSR(val);
                    WriteMem(addr, val);
                    break;

                case 0x60:
                    InstrRTS();
                    break;
                case 0x61:
                    val = AddrModeZeroPageIndexedXIndirectValue();
                    InstrADC(val);
                    break;
                case 0x65:
                    val = AddrModeZeroPageValue();
                    InstrADC(val);
                    break;
                case 0x66:
                    addr = AddrModeZeroPageAddress();
                    val = ReadMem(addr);
                    val = InstrROR(val);
                    WriteMem(addr, val);
                    break;
                case 0x68:
                    InstrPLA();
                    break;
                case 0x69:
                    val = AddrModeImmediateValue();
                    InstrADC(val);
                    break;
                case 0x6a:
                    val = this.regA;
                    val = InstrROR(val);
                    this.regA = val;
                    break;
                case 0x6c:
                    addr = AddrModeAbsoluteIndirectAddress();
                    InstrJMP(addr);
                    break;
                case 0x6d:
                    val = AddrModeAbsoluteValue();
                    InstrADC(val);
                    break;
                case 0x6e:
                    addr = AddrModeAbsoluteAddress();
                    val = ReadMem(addr);
                    val = InstrROR(val);
                    WriteMem(addr, val);
                    break;

                case 0x70:
                    addr = AddrModeRelativeAddress();
                    InstrBVS(addr);
                    break;
                case 0x71:
                    val = AddrModeZeroPageIndirectIndexedYValue();
                    InstrADC(val);
                    break;
                case 0x75:
                    val = AddrModeZeroPageIndexedXValue();
                    InstrADC(val);
                    break;
                case 0x76:
                    addr = AddrModeZeroPageIndexedXAddress();
                    val = ReadMem(addr);
                    val = InstrROR(val);
                    WriteMem(addr, val);
                    break;
                case 0x78:
                    InstrSEI();
                    break;
                case 0x79:
                    val = AddrModeAbsoluteIndexedYValue();
                    InstrADC(val);
                    break;
                case 0x7d:
                    val = AddrModeAbsoluteIndexedXValue();
                    InstrADC(val);
                    break;
                case 0x7e:
                    addr = AddrModeAbsoluteIndexedXAddress();
                    val = ReadMem(addr);
                    val = InstrROR(val);
                    WriteMem(addr, val);
                    break;

                case 0x81:
                    addr = AddrModeZeroPageIndexedXIndirectAddress();
                    InstrSTA(addr);
                    break;
                case 0x84:
                    addr = AddrModeZeroPageAddress();
                    InstrSTY(addr);
                    break;
                case 0x85:
                    addr = AddrModeZeroPageAddress();
                    InstrSTA(addr);
                    break;
                case 0x86:
                    addr = AddrModeZeroPageAddress();
                    InstrSTX(addr);
                    break;
                case 0x88:
                    InstrDEY();
                    break;
                case 0x8a:
                    InstrTXA();
                    break;
                case 0x8c:
                    addr = AddrModeAbsoluteAddress();
                    InstrSTY(addr);
                    break;
                case 0x8d:
                    addr = AddrModeAbsoluteAddress();
                    InstrSTA(addr);
                    break;
                case 0x8e:
                    addr = AddrModeAbsoluteAddress();
                    InstrSTX(addr);
                    break;

                case 0x90:
                    addr = AddrModeRelativeAddress();
                    InstrBCC(addr);
                    break;
                case 0x91:
                    addr = AddrModeZeroPageIndirectIndexedYAddress();
                    InstrSTA(addr);
                    break;
                case 0x94:
                    addr = AddrModeZeroPageIndexedXAddress();
                    InstrSTY(addr);
                    break;
                case 0x95:
                    addr = AddrModeZeroPageIndexedXAddress();
                    InstrSTA(addr);
                    break;
                case 0x96:
                    addr = AddrModeZeroPageIndexedYAddress();
                    InstrSTX(addr);
                    break;
                case 0x98:
                    InstrTYA();
                    break;
                case 0x99:
                    addr = AddrModeAbsoluteIndexedYAddress();
                    InstrSTA(addr);
                    break;
                case 0x9a:
                    InstrTXS();
                    break;
                case 0x9d:
                    addr = AddrModeAbsoluteIndexedXAddress();
                    InstrSTA(addr);
                    break;

                case 0xa0:
                    val = AddrModeImmediateValue();
                    InstrLDY(val);
                    break;
                case 0xa1:
                    val = AddrModeZeroPageIndexedXIndirectValue();
                    InstrLDA(val);
                    break;
                case 0xa2:
                    val = AddrModeImmediateValue();
                    InstrLDX(val);
                    break;
                case 0xa4:
                    val = AddrModeZeroPageValue();
                    InstrLDY(val);
                    break;
                case 0xa5:
                    val = AddrModeZeroPageValue();
                    InstrLDA(val);
                    break;
                case 0xa6:
                    val = AddrModeZeroPageValue();
                    InstrLDX(val);
                    break;
                case 0xa8:
                    InstrTAY();
                    break;
                case 0xa9:
                    val = AddrModeImmediateValue();
                    InstrLDA(val);
                    break;
                case 0xaa:
                    InstrTAX();
                    break;
                case 0xac:
                    val = AddrModeAbsoluteValue();
                    InstrLDY(val);
                    break;
                case 0xad:
                    val = AddrModeAbsoluteValue();
                    InstrLDA(val);
                    break;
                case 0xae:
                    val = AddrModeAbsoluteValue();
                    InstrLDX(val);
                    break;

                case 0xb0:
                    addr = AddrModeRelativeAddress();
                    InstrBCS(addr);
                    break;
                case 0xb1:
                    val = AddrModeZeroPageIndirectIndexedYValue();
                    InstrLDA(val);
                    break;
                case 0xb4:
                    val = AddrModeZeroPageIndexedXValue();
                    InstrLDY(val);
                    break;
                case 0xb5:
                    val = AddrModeZeroPageIndexedXValue();
                    InstrLDA(val);
                    break;
                case 0xb6:
                    val = AddrModeZeroPageIndexedYValue();
                    InstrLDX(val);
                    break;
                case 0xb8:
                    InstrCLV();
                    break;
                case 0xb9:
                    val = AddrModeAbsoluteIndexedYValue();
                    InstrLDA(val);
                    break;
                case 0xba:
                    InstrTSX();
                    break;
                case 0xbc:
                    val = AddrModeAbsoluteIndexedXValue();
                    InstrLDY(val);
                    break;
                case 0xbd:
                    val = AddrModeAbsoluteIndexedXValue();
                    InstrLDA(val);
                    break;
                case 0xbe:
                    val = AddrModeAbsoluteIndexedYValue();
                    InstrLDX(val);
                    break;

                case 0xc0:
                    val = AddrModeImmediateValue();
                    InstrCPY(val);
                    break;
                case 0xc1:
                    val = AddrModeZeroPageIndexedXIndirectValue();
                    InstrCMP(val);
                    break;
                case 0xc4:
                    val = AddrModeZeroPageValue();
                    InstrCPY(val);
                    break;
                case 0xc5:
                    val = AddrModeZeroPageValue();
                    InstrCMP(val);
                    break;
                case 0xc6:
                    addr = AddrModeZeroPageAddress();
                    InstrDEC(addr);
                    break;
                case 0xc8:
                    InstrINY();
                    break;
                case 0xc9:
                    val = AddrModeImmediateValue();
                    InstrCMP(val);
                    break;
                case 0xca:
                    InstrDEX();
                    break;
                case 0xcc:
                    val = AddrModeAbsoluteValue();
                    InstrCPY(val);
                    break;
                case 0xcd:
                    val = AddrModeAbsoluteValue();
                    InstrCMP(val);
                    break;
                case 0xce:
                    addr = AddrModeAbsoluteAddress();
                    InstrDEC(addr);
                    break;

                case 0xd0:
                    addr = AddrModeRelativeAddress();
                    InstrBNE(addr);
                    break;
                case 0xd1:
                    val = AddrModeZeroPageIndirectIndexedYValue();
                    InstrCMP(val);
                    break;
                case 0xd5:
                    val = AddrModeZeroPageIndexedXValue();
                    InstrCMP(val);
                    break;
                case 0xd6:
                    addr = AddrModeZeroPageIndexedXAddress();
                    InstrDEC(addr);
                    break;
                case 0xd8:
                    InstrCLD();
                    break;
                case 0xd9:
                    val = AddrModeAbsoluteIndexedYValue();
                    InstrCMP(val);
                    break;
                case 0xdd:
                    val = AddrModeAbsoluteIndexedXValue();
                    InstrCMP(val);
                    break;
                case 0xde:
                    addr = AddrModeAbsoluteIndexedXAddress();
                    InstrDEC(addr);
                    break;

                case 0xe0:
                    val = AddrModeImmediateValue();
                    InstrCPX(val);
                    break;
                case 0xe1:
                    val = AddrModeZeroPageIndexedXIndirectValue();
                    InstrSBC(val);
                    break;
                case 0xe4:
                    val = AddrModeZeroPageValue();
                    InstrCPX(val);
                    break;
                case 0xe5:
                    val = AddrModeZeroPageValue();
                    InstrSBC(val);
                    break;
                case 0xe6:
                    addr = AddrModeZeroPageAddress();
                    InstrINC(addr);
                    break;
                case 0xe8:
                    InstrINX();
                    break;
                case 0xe9:
                    val = AddrModeImmediateValue();
                    InstrSBC(val);
                    break;
                case 0xea:
                    InstrNOP();
                    break;
                case 0xec:
                    val = AddrModeAbsoluteValue();
                    InstrCPX(val);
                    break;
                case 0xed:
                    val = AddrModeAbsoluteValue();
                    InstrSBC(val);
                    break;
                case 0xee:
                    addr = AddrModeAbsoluteAddress();
                    InstrINC(addr);
                    break;

                case 0xf0:
                    addr = AddrModeRelativeAddress();
                    InstrBEQ(addr);
                    break;
                case 0xf1:
                    val = AddrModeZeroPageIndirectIndexedYValue();
                    InstrSBC(val);
                    break;
                case 0xf5:
                    val = AddrModeZeroPageIndexedXValue();
                    InstrSBC(val);
                    break;
                case 0xf6:
                    addr = AddrModeZeroPageIndexedXAddress();
                    InstrINC(addr);
                    break;
                case 0xf8:
                    InstrSED();
                    break;
                case 0xf9:
                    val = AddrModeAbsoluteIndexedYValue();
                    InstrSBC(val);
                    break;
                case 0xfd:
                    val = AddrModeAbsoluteIndexedXValue();
                    InstrSBC(val);
                    break;
                case 0xfe:
                    addr = AddrModeAbsoluteIndexedXAddress();
                    InstrINC(addr);
                    break;

                // opcode invalide !
                default:
                    switch (this.uoPolicy) {
                        case UnknownOpcodePolicy.ThrowException:
                            throw new UnknownOpcodeException(
                                    this.regPC,
                                    opcode,
                                    String.Format(ERR_UNKNOWN_OPCODE,
                                                  this.regPC, opcode));
                        case UnknownOpcodePolicy.DoNop:
                            InstrNOP();
                            break;
                        case UnknownOpcodePolicy.Emulate:
                            // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                            break;
                    }
                    break;
            }

            // comptage des cycles écoulés
            ulong cycEnd = this.cycles;
            return cycEnd - cycBegin;
        }

        /// <summary>
        /// Lance l'exécution du processeur pendant AU MOINS
        /// le nombre de cycles passé en paramètre.
        /// <br/>
        /// En effet : toute instruction entamée est terminée
        /// (y compris les éventuelles réponses aux interruptions).
        /// Ainsi, le nombre de cycles exécutés peut être égal ou
        /// supérieur au nombre voulu.
        /// </summary>
        /// <param name="numCycles">
        /// Nombre de cycles processeur à exécuter.
        /// </param>
        /// <returns>
        /// Le nombre de cycles processeur réellement exécutés.
        /// </returns>
        public ulong Run(ulong numCycles)
        {
            ulong cycCount = 0L;

            while (cycCount < numCycles) {
                cycCount += Step();
            }

            return cycCount;
        }


        /* ====================== PROPRIÉTÉS PUBLIQUES ====================== */

        /// <summary>
        /// Objet espace-mémoire attaché à ce processeur lors de sa création.
        /// (Propriété en lecture seule.)
        /// </summary>
        public IMemorySpace6502 MemorySpace
        {
            get { return this.memSpace; }
        }


        /// <summary>
        /// Accès au registre A (Accumulateur) du processeur.
        /// </summary>
        public Byte RegisterA
        {
            get { return this.regA; }
            set { this.regA = value; }
        }

        /// <summary>
        /// Accès au registre d'index X du processeur.
        /// </summary>
        public Byte RegisterX
        {
            get { return this.regX; }
            set { this.regX = value; }
        }

        /// <summary>
        /// Accès au registre d'index Y du processeur.
        /// </summary>
        public Byte RegisterY
        {
            get { return this.regY; }
            set { this.regY = value; }
        }

        /// <summary>
        /// Accès au registre S ("Stack pointer", pointeur de pile)
        /// du processeur.
        /// </summary>
        public Byte RegisterS
        {
            get { return this.regS; }
            set { this.regS = value; }
        }

        /// <summary>
        /// Accès au registre PC ("Program Counter", compteur programme
        /// alias compteur ordinal) du processeur.
        /// </summary>
        public UInt16 RegisterPC
        {
            get { return this.regPC; }
            set { this.regPC = value; }
        }

        /// <summary>
        /// Accès au registre P (de statut) du processeur.
        /// </summary>
        public Byte RegisterP
        {
            // le contenu de ce registre est calculé à la volée
            // en fonction des "flags"
            get {
                byte p = 0x20;
                if (this.flagN) p |= FLAG_N;
                if (this.flagV) p |= FLAG_V;
                if (this.flagB) p |= FLAG_B;
                if (this.flagD) p |= FLAG_D;
                if (this.flagI) p |= FLAG_I;
                if (this.flagZ) p |= FLAG_Z;
                if (this.flagC) p |= FLAG_C;
                return p;
            }
            set {
                this.flagN = ((value & FLAG_N) != 0);
                this.flagV = ((value & FLAG_V) != 0);
                this.flagB = ((value & FLAG_B) != 0);
                this.flagD = ((value & FLAG_D) != 0);
                this.flagI = ((value & FLAG_I) != 0);
                this.flagZ = ((value & FLAG_Z) != 0);
                this.flagC = ((value & FLAG_C) != 0);
            }
        }

        /// <summary>
        /// Flag C ("Carry", retenue) dans le registre de statut du processeur.
        /// </summary>
        public Boolean FlagC
        {
            get { return this.flagC; }
            set { this.flagC = value; }
        }

        /// <summary>
        /// Flag Z (Zéro) dans le registre de statut du processeur.
        /// </summary>
        public Boolean FlagZ
        {
            get { return this.flagZ; }
            set { this.flagZ = value; }
        }

        /// <summary>
        /// Flag I (Interruptions masquées) dans le registre de statut
        /// du processeur.
        /// </summary>
        public Boolean FlagI
        {
            get { return this.flagI; }
            set { this.flagI = value; }
        }

        /// <summary>
        /// Flag D (Décimal) dans le registre de statut du processeur.
        /// </summary>
        public Boolean FlagD
        {
            get { return this.flagD; }
            set { this.flagD = value; }
        }

        /// <summary>
        /// Flag B ("Break", interruption logiciells) dans le registre de statut
        /// du processeur.
        /// </summary>
        public Boolean FlagB
        {
            get { return this.flagB; }
            set { this.flagB = value; }
        }

        /// <summary>
        /// Flag V ("oVerflow", débordement) dans le registre de statut
        /// du processeur.
        /// </summary>
        public Boolean FlagV
        {
            get { return this.flagV; }
            set { this.flagV = value; }
        }

        /// <summary>
        /// Flag N (Négatif) dans le registre de statut du processeur.
        /// </summary>
        public Boolean FlagN
        {
            get { return this.flagN; }
            set { this.flagN = value; }
        }


        /// <summary>
        /// Nombre de cycles écoulés lors du fonctionnement du processeur.
        /// (Propriété en lecture seule.)
        /// </summary>
        public UInt64 ElapsedCycles
        {
            get { return this.cycles; }
        }


        /// <summary>
        /// Ligne de réinitialisation du processeur.
        /// Cette ligne est sensible au niveau.
        /// </summary>
        public Boolean ResetLine
        {
            get { return this.resetLine; }
            set {
                if (value) Reset();
                this.resetLine = value;
            }
        }

        /// <summary>
        /// Ligne de requête d'interruption matérielle non-masquable.
        /// Cette ligne est sensible à la transition.
        /// </summary>
        private Boolean NMILine
        {
            get { return this.nmiLine; }
            set {
                if (value & !nmiLine) TriggerNMI();
                this.nmiLine = value;
            }
        }

        /// <summary>
        /// Ligne de requête d'interruption matérielle (masquable).
        /// Cette ligne est sensible au niveau.
        /// </summary>
        private Boolean IRQLine
        {
            get { return this.irqLine; }
            set { this.irqLine = value; }
        }


        /// <summary>
        /// Politique de prise en charge des opcodes invalides à l'exécution.
        /// </summary>
        public UnknownOpcodePolicy InvalidOpcodePolicy
        {
            get { return this.uoPolicy; }
            set { this.uoPolicy = value; }
        }

    }
}


