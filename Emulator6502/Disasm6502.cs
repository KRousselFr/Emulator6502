using System;
using System.Text;


namespace Emulator6502
{
    /// <summary>
    /// Classe désassemblant de le code machine des processeurs
    /// de la famille 65x02.
    /// </summary>
    public class Disasm6502
    {
        /* ========================= TYPES IMBRIQUES ======================== */

        public enum ProcessorLevel
        {
            /// Processeur original 6502 en technologie NMOS (MOS, 1975-1977)
            NMOS6502,

            /// Première version CMOS du processeur 65C02 (WDC, 1981)
            WDC65C02,

            /// Deuxième version CMOS du processeur 6502 (Rockwell, 1983)
            R65C02,

            /// Version CMOS synthétisable actuelle du 6502 (WDC, depuis 1986)
            WDC65C02S
        }


        /* =========================== CONSTANTES =========================== */

        // messages affichés
        private const String ERR_UNREADABLE_ADDRESS =
                "Impossible de lire le contenu de l'adresse ${0:X4} !";
        private const String ERR_UNWRITABLE_ADDRESS =
                "Impossible d'écrire la valeur $1:X2 à l'adresse ${0:X4} !";
        private const String ERR_UNKNOWN_OPCODE =
                "Opcode invalide (${1:X2}) rencontré à l'adresse ${0:X4} !";


        /* ========================== CHAMPS PRIVÉS ========================= */

        // espace-mémoire attaché au processeur
        // (défini une fois pour toutes à la construction)
        private readonly IMemorySpace6502 memSpace;

        // politique vis-à-vis des opcodes invalides
        private UnknownOpcodePolicy uoPolicy;

        // niveau du processeur dont les instructions sont à désassembler
        private ProcessorLevel procLevel;

        // adresse courante de l'instruction en cours de désassemblage
        private int regPC;


        /* ========================== CONSTRUCTEUR ========================== */

        /// <summary>
        /// Constructeur de référence (et unique) de la classe Disasm6502.
        /// </summary>
        /// <param name="memorySpace">
        /// Espace-mémoire où lire le code binaire à desassembler.
        /// </param>
        public Disasm6502(IMemorySpace6502 memorySpace)
        {
            this.memSpace = memorySpace;
            this.uoPolicy = UnknownOpcodePolicy.DoNop;
            this.procLevel = ProcessorLevel.NMOS6502;
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

        private byte ReadMem(int addr)
        {
            byte? memval = this.memSpace.ReadMemory((ushort)addr);
            if (!(memval.HasValue)) {
                throw new AddressUnreadableException(
                        addr,
                        String.Format(ERR_UNREADABLE_ADDRESS,
                                      addr));
            }
            return memval.Value;
        }

        /* ~~~~ implantation des modes d'adressage ~~~~ */

        /* mode d'adressage immédiat : INSTR #nn  */
        private string AddrModeImmediate()
        {
            byte val = ReadMem(this.regPC);
            this.regPC++;
            return String.Format("#${0:X2}", val);
        }

        /* mode d'adressage absolu : INSTR $xxxx  */
        private string AddrModeAbsolute()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            this.regPC++;
            ushort addr = MakeWord(hi, lo);
            return String.Format("${0:X4}", addr);
        }

        /* mode d'adressage absolu indexé sur X : INSTR $xxxx, X  */
        private string AddrModeAbsoluteIndexedX()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            this.regPC++;
            ushort addr = MakeWord(hi, lo);
            return String.Format("${0:X4}, X", addr);
        }

        /* mode d'adressage absolu indexé sur Y : INSTR $xxxx, Y  */
        private string AddrModeAbsoluteIndexedY()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            this.regPC++;
            ushort addr = MakeWord(hi, lo);
            return String.Format("${0:X4}, Y", addr);
        }

        /* mode d'adressage absolu indirect : INSTR ($xxxx)  */
        private string AddrModeAbsoluteIndirect()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            this.regPC++;
            ushort addr = MakeWord(hi, lo);
            return String.Format("(${0:X4})", addr);
        }

        /* mode d'adressage absolu indexé sur X indirect : INSTR ($xxxx, X) */
        private string AddrModeAbsoluteIndexedXIndirect()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            byte hi = ReadMem(this.regPC);
            this.regPC++;
            ushort addr = MakeWord(hi, lo);
            return String.Format("(${0:X4}, X)", addr);
            ;
        }

        /* mode d'adressage page-zéro : INSTR $xx  */
        private string AddrModeZeroPage()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            return String.Format("${0:X2}", lo);
        }

        /* mode d'adressage page-zéro indexé sur X : INSTR $xx, X  */
        private string AddrModeZeroPageIndexedX()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            return String.Format("${0:X2}, X", lo);
        }

        /* mode d'adressage page-zéro indexé sur Y : INSTR $xx, Y  */
        private string AddrModeZeroPageIndexedY()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            return String.Format("${0:X2}, Y", lo);
        }

        private string AddrModeZeroPageIndirect()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            return String.Format("(${0:X2})", lo);
        }

        /* mode d'adressage page-zéro indexé sur X indirect : INSTR ($xx, X)  */
        private string AddrModeZeroPageIndexedXIndirect()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            return String.Format("(${0:X2}, X)", lo);
        }

        /* mode d'adressage page-zéro indirect indexé sur Y : INSTR ($xx), Y  */
        private string AddrModeZeroPageIndirectIndexedY()
        {
            byte lo = ReadMem(this.regPC);
            this.regPC++;
            return String.Format("(${0:X2}), Y", lo);
        }

        /* mode d'adressage relatif : Bxx ±nnn  */
        private string AddrModeRelative()
        {
            sbyte dpl = (sbyte)(ReadMem(this.regPC));
            this.regPC++;
            ushort addr = (ushort)(this.regPC + dpl);
            return String.Format("{0:+000;-000}  (>${1:X4})", dpl, addr);
        }


        /* ======================= MÉTHODES PUBLIQUES ======================= */

        /// <summary>
        /// Désassemble une instruction en mémoire.
        /// </summary>
        /// <param name="memoryAddress">
        /// Adresse où débute l'instruction à désassembler.
        /// </param>
        /// <returns></returns>
        /// <exception cref="AddressUnreadableException">
        /// Si l'une des adresses-mémoire à traiter est impossible à lire.
        /// </exception>
        public String DisassembleInstructionAt(ushort memoryAddress)
        {
            StringBuilder sbResult = new StringBuilder();
            this.regPC = memoryAddress;

            /* écrit d'abord l'adresse traitée */
            sbResult.Append(String.Format("{0:X4} : ", this.regPC));

            /* analyse l'opcode trouvé à cette adresse */
            byte opcode = ReadMem(this.regPC);
            this.regPC++;
            string mnemo = "", args = "";
            switch (opcode) {
                case 0x00:
                    mnemo = "BRK";
                    break;
                case 0x01:
                    mnemo = "ORA";
                    args = AddrModeZeroPageIndexedXIndirect();
                    break;
                case 0x04:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "TSB";
                    args = AddrModeZeroPage();
                    break;
                case 0x05:
                    mnemo = "ORA";
                    args = AddrModeZeroPage();
                    break;
                case 0x06:
                    mnemo = "ASL";
                    args = AddrModeZeroPage();
                    break;
                case 0x07:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "RMB 0,";
                    args = AddrModeZeroPage();
                    break;
                case 0x08:
                    mnemo = "PHP";
                    break;
                case 0x09:
                    mnemo = "ORA";
                    args = AddrModeImmediate();
                    break;
                case 0x0a:
                    mnemo = "ASL";
                    args = "A";
                    break;
                case 0x0c:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "TSB";
                    args = AddrModeAbsolute();
                    break;
                case 0x0d:
                    mnemo = "ORA";
                    args = AddrModeAbsolute();
                    break;
                case 0x0e:
                    mnemo = "ASL";
                    args = AddrModeAbsolute();
                    break;
                case 0x0f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBR 0,";
                    args = AddrModeRelative();
                    break;

                case 0x10:
                    mnemo = "BPL";
                    args = AddrModeRelative();
                    break;
                case 0x11:
                    mnemo = "ORA";
                    args = AddrModeZeroPageIndirectIndexedY();
                    break;
                case 0x12:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "ORA";
                    args = AddrModeZeroPageIndirect();
                    break;
                case 0x14:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "TRB";
                    args = AddrModeZeroPage();
                    break;
                case 0x15:
                    mnemo = "ORA";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x16:
                    mnemo = "ASL";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x17:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "RMB 1,";
                    args = AddrModeZeroPage();
                    break;
                case 0x18:
                    mnemo = "CLC";
                    break;
                case 0x19:
                    mnemo = "ORA";
                    args = AddrModeAbsoluteIndexedY();
                    break;
                case 0x1a:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "INC";
                    args = "A";
                    break;
                case 0x1c:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "TRB";
                    args = AddrModeAbsolute();
                    break;
                case 0x1d:
                    mnemo = "ORA";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x1e:
                    mnemo = "ASL";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x1f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBR 1,";
                    args = AddrModeRelative();
                    break;

                case 0x20:
                    mnemo = "JSR";
                    args = AddrModeAbsolute();
                    break;
                case 0x21:
                    mnemo = "AND";
                    args = AddrModeZeroPageIndexedXIndirect();
                    break;
                case 0x24:
                    mnemo = "BIT";
                    args = AddrModeZeroPage();
                    break;
                case 0x25:
                    mnemo = "AND";
                    args = AddrModeZeroPage();
                    break;
                case 0x26:
                    mnemo = "ROL";
                    args = AddrModeZeroPage();
                    break;
                case 0x27:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "RMB 2,";
                    args = AddrModeZeroPage();
                    break;
                case 0x28:
                    mnemo = "PLP";
                    break;
                case 0x29:
                    mnemo = "AND";
                    args = AddrModeImmediate();
                    break;
                case 0x2a:
                    mnemo = "ROL";
                    args = "A";
                    break;
                case 0x2c:
                    mnemo = "BIT";
                    args = AddrModeAbsolute();
                    break;
                case 0x2d:
                    mnemo = "AND";
                    args = AddrModeAbsolute();
                    break;
                case 0x2e:
                    mnemo = "ROL";
                    args = AddrModeAbsolute();
                    break;
                case 0x2f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBR 2,";
                    args = AddrModeRelative();
                    break;

                case 0x30:
                    mnemo = "BMI";
                    args = AddrModeRelative();
                    break;
                case 0x31:
                    mnemo = "AND";
                    args = AddrModeZeroPageIndirectIndexedY();
                    break;
                case 0x32:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "AND";
                    args = AddrModeZeroPageIndirect();
                    break;
                case 0x34:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "BIT";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x35:
                    mnemo = "AND";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x36:
                    mnemo = "ROL";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x37:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "RMB 3,";
                    args = AddrModeZeroPage();
                    break;
                case 0x38:
                    mnemo = "SEC";
                    break;
                case 0x39:
                    mnemo = "AND";
                    args = AddrModeAbsoluteIndexedY();
                    break;
                case 0x3a:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "DEC";
                    args = "A";
                    break;
                case 0x3c:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "BIT";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x3d:
                    mnemo = "AND";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x3e:
                    mnemo = "ROL";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x3f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBR 3,";
                    args = AddrModeRelative();
                    break;

                case 0x40:
                    mnemo = "RTI";
                    break;
                case 0x41:
                    mnemo = "EOR";
                    args = AddrModeZeroPageIndexedXIndirect();
                    break;
                case 0x45:
                    mnemo = "EOR";
                    args = AddrModeZeroPage();
                    break;
                case 0x46:
                    mnemo = "LSR";
                    args = AddrModeZeroPage();
                    break;
                case 0x47:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "RMB 4,";
                    args = AddrModeZeroPage();
                    break;
                case 0x48:
                    mnemo = "PHA";
                    break;
                case 0x49:
                    mnemo = "EOR";
                    args = AddrModeImmediate();
                    break;
                case 0x4a:
                    mnemo = "LSR";
                    args = "A";
                    break;
                case 0x4c:
                    mnemo = "JMP";
                    args = AddrModeAbsolute();
                    break;
                case 0x4d:
                    mnemo = "EOR";
                    args = AddrModeAbsolute();
                    break;
                case 0x4e:
                    mnemo = "LSR";
                    args = AddrModeAbsolute();
                    break;
                case 0x4f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBR 4,";
                    args = AddrModeRelative();
                    break;

                case 0x50:
                    mnemo = "BVC";
                    args = AddrModeRelative();
                    break;
                case 0x51:
                    mnemo = "EOR";
                    args = AddrModeZeroPageIndirectIndexedY();
                    break;
                case 0x52:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "EOR";
                    args = AddrModeZeroPageIndirect();
                    break;
                case 0x55:
                    mnemo = "EOR";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x56:
                    mnemo = "LSR";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x57:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "RMB 5,";
                    args = AddrModeZeroPage();
                    break;
                case 0x58:
                    mnemo = "CLI";
                    break;
                case 0x59:
                    mnemo = "EOR";
                    args = AddrModeAbsoluteIndexedY();
                    break;
                case 0x5a:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "PHY";
                    break;
                case 0x5d:
                    mnemo = "EOR";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x5e:
                    mnemo = "LSR";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x5f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBR 5,";
                    args = AddrModeRelative();
                    break;

                case 0x60:
                    mnemo = "RTS";
                    break;
                case 0x61:
                    mnemo = "ADC";
                    args = AddrModeZeroPageIndexedXIndirect();
                    break;
                case 0x64:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "STZ";
                    args = AddrModeZeroPage();
                    break;
                case 0x65:
                    mnemo = "ADC";
                    args = AddrModeZeroPage();
                    break;
                case 0x66:
                    mnemo = "ROR";
                    args = AddrModeZeroPage();
                    break;
                case 0x67:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "RMB 6,";
                    args = AddrModeZeroPage();
                    break;
                case 0x68:
                    mnemo = "PLA";
                    break;
                case 0x69:
                    mnemo = "ADC";
                    args = AddrModeImmediate();
                    break;
                case 0x6a:
                    mnemo = "ROR";
                    args = "A";
                    break;
                case 0x6c:
                    mnemo = "JMP";
                    args = AddrModeAbsoluteIndirect();
                    break;
                case 0x6d:
                    mnemo = "ADC";
                    args = AddrModeAbsolute();
                    break;
                case 0x6e:
                    mnemo = "ROR";
                    args = AddrModeAbsolute();
                    break;
                case 0x6f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBR 6,";
                    args = AddrModeRelative();
                    break;

                case 0x70:
                    mnemo = "BVS";
                    args = AddrModeRelative();
                    break;
                case 0x71:
                    mnemo = "ADC";
                    args = AddrModeZeroPageIndirectIndexedY();
                    break;
                case 0x72:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "ADC";
                    args = AddrModeZeroPageIndirect();
                    break;
                case 0x74:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "STZ";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x75:
                    mnemo = "ADC";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x76:
                    mnemo = "ROR";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x77:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "RMB 7,";
                    args = AddrModeZeroPage();
                    break;
                case 0x78:
                    mnemo = "SEI";
                    break;
                case 0x79:
                    mnemo = "ADC";
                    args = AddrModeAbsoluteIndexedY();
                    break;
                case 0x7a:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "PLY";
                    break;
                case 0x7c:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "JMP";
                    args = AddrModeAbsoluteIndexedXIndirect();
                    break;
                case 0x7d:
                    mnemo = "ADC";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x7e:
                    mnemo = "ROR";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x7f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBR 7,";
                    args = AddrModeRelative();
                    break;

                case 0x80:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "BRA";
                    args = AddrModeRelative();
                    break;
                case 0x81:
                    mnemo = "STA";
                    args = AddrModeZeroPageIndexedXIndirect();
                    break;
                case 0x84:
                    mnemo = "STY";
                    args = AddrModeZeroPage();
                    break;
                case 0x85:
                    mnemo = "STA";
                    args = AddrModeZeroPage();
                    break;
                case 0x86:
                    mnemo = "STX";
                    args = AddrModeZeroPage();
                    break;
                case 0x87:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "SMB 0,";
                    args = AddrModeZeroPage();
                    break;
                case 0x88:
                    mnemo = "DEY";
                    break;
                case 0x89:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "BIT";
                    args = AddrModeImmediate();
                    break;
                case 0x8a:
                    mnemo = "TXA";
                    break;
                case 0x8c:
                    mnemo = "STY";
                    args = AddrModeAbsolute();
                    break;
                case 0x8d:
                    mnemo = "STA";
                    args = AddrModeAbsolute();
                    break;
                case 0x8e:
                    mnemo = "STX";
                    args = AddrModeAbsolute();
                    break;
                case 0x8f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBS 0,";
                    args = AddrModeRelative();
                    break;

                case 0x90:
                    mnemo = "BCC";
                    args = AddrModeRelative();
                    break;
                case 0x91:
                    mnemo = "STA";
                    args = AddrModeZeroPageIndirectIndexedY();
                    break;
                case 0x92:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "STA";
                    args = AddrModeZeroPageIndirect();
                    break;
                case 0x94:
                    mnemo = "STY";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x95:
                    mnemo = "STA";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0x96:
                    mnemo = "STX";
                    args = AddrModeZeroPageIndexedY();
                    break;
                case 0x97:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "SMB 1,";
                    args = AddrModeZeroPage();
                    break;
                case 0x98:
                    mnemo = "TYA";
                    break;
                case 0x99:
                    mnemo = "STA";
                    args = AddrModeAbsoluteIndexedY();
                    break;
                case 0x9a:
                    mnemo = "TXS";
                    break;
                case 0x9c:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "STZ";
                    args = AddrModeAbsolute();
                    break;
                case 0x9d:
                    mnemo = "STA";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x9e:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "STZ";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0x9f:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBS 1,";
                    args = AddrModeRelative();
                    break;

                case 0xa0:
                    mnemo = "LDY";
                    args = AddrModeImmediate();
                    break;
                case 0xa1:
                    mnemo = "LDA";
                    args = AddrModeZeroPageIndexedXIndirect();
                    break;
                case 0xa2:
                    mnemo = "LDX";
                    args = AddrModeImmediate();
                    break;
                case 0xa4:
                    mnemo = "LDY";
                    args = AddrModeZeroPage();
                    break;
                case 0xa5:
                    mnemo = "LDA";
                    args = AddrModeZeroPage();
                    break;
                case 0xa6:
                    mnemo = "LDX";
                    args = AddrModeZeroPage();
                    break;
                case 0xa7:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "SMB 2,";
                    args = AddrModeZeroPage();
                    break;
                case 0xa8:
                    mnemo = "TAY";
                    break;
                case 0xa9:
                    mnemo = "LDA";
                    args = AddrModeImmediate();
                    break;
                case 0xaa:
                    mnemo = "TAX";
                    break;
                case 0xac:
                    mnemo = "LDY";
                    args = AddrModeAbsolute();
                    break;
                case 0xad:
                    mnemo = "LDA";
                    args = AddrModeAbsolute();
                    break;
                case 0xae:
                    mnemo = "LDX";
                    args = AddrModeAbsolute();
                    break;
                case 0xaf:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBS 2,";
                    args = AddrModeRelative();
                    break;

                case 0xb0:
                    mnemo = "BCS";
                    args = AddrModeRelative();
                    break;
                case 0xb1:
                    mnemo = "LDA";
                    args = AddrModeZeroPageIndirectIndexedY();
                    break;
                case 0xb2:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "LDA";
                    args = AddrModeZeroPageIndirect();
                    break;
                case 0xb4:
                    mnemo = "LDY";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0xb5:
                    mnemo = "LDA";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0xb6:
                    mnemo = "LDX";
                    args = AddrModeZeroPageIndexedY();
                    break;
                case 0xb7:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "SMB 3,";
                    args = AddrModeZeroPage();
                    break;
                case 0xb8:
                    mnemo = "CLV";
                    break;
                case 0xb9:
                    mnemo = "LDA";
                    args = AddrModeAbsoluteIndexedY();
                    break;
                case 0xba:
                    mnemo = "TSX";
                    break;
                case 0xbc:
                    mnemo = "LDY";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0xbd:
                    mnemo = "LDA";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0xbe:
                    mnemo = "LDX";
                    args = AddrModeAbsoluteIndexedY();
                    break;
                case 0xbf:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBS 3,";
                    args = AddrModeRelative();
                    break;

                case 0xc0:
                    mnemo = "CPY";
                    args = AddrModeImmediate();
                    break;
                case 0xc1:
                    mnemo = "CMP";
                    args = AddrModeZeroPageIndexedXIndirect();
                    break;
                case 0xc4:
                    mnemo = "CPY";
                    args = AddrModeZeroPage();
                    break;
                case 0xc5:
                    mnemo = "CMP";
                    args = AddrModeZeroPage();
                    break;
                case 0xc6:
                    mnemo = "DEC";
                    args = AddrModeZeroPage();
                    break;
                case 0xc7:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "SMB 4,";
                    args = AddrModeZeroPage();
                    break;
                case 0xc8:
                    mnemo = "INY";
                    break;
                case 0xc9:
                    mnemo = "CMP";
                    args = AddrModeImmediate();
                    break;
                case 0xca:
                    mnemo = "DEX";
                    break;
                case 0xcb:
                    // instruction d'économie d'énergie, présente dans
                    // la version actuelle du processeur seulement
                    if (this.procLevel != ProcessorLevel.WDC65C02S)
                    {
                        goto default;
                    }
                    mnemo = "WAI";
                    break;
                case 0xcc:
                    mnemo = "CPY";
                    args = AddrModeAbsolute();
                    break;
                case 0xcd:
                    mnemo = "CMP";
                    args = AddrModeAbsolute();
                    break;
                case 0xce:
                    mnemo = "DEC";
                    args = AddrModeAbsolute();
                    break;
                case 0xcf:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02))
                    {
                        goto default;
                    }
                    mnemo = "BBS 4,";
                    args = AddrModeRelative();
                    break;

                case 0xd0:
                    mnemo = "BNE";
                    args = AddrModeRelative();
                    break;
                case 0xd1:
                    mnemo = "CMP";
                    args = AddrModeZeroPageIndirectIndexedY();
                    break;
                case 0xd2:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "CMP";
                    args = AddrModeZeroPageIndirect();
                    break;
                case 0xd5:
                    mnemo = "CMP";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0xd6:
                    mnemo = "DEC";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0xd7:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "SMB 5,";
                    args = AddrModeZeroPage();
                    break;
                case 0xd8:
                    mnemo = "CLD";
                    break;
                case 0xd9:
                    mnemo = "CMP";
                    args = AddrModeAbsoluteIndexedY();
                    break;
                case 0xda:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "PHX";
                    break;
                case 0xdb:
                    // instruction d'économie d'énergie, présente dans
                    // la version actuelle du processeur seulement
                    if (this.procLevel != ProcessorLevel.WDC65C02S)
                    {
                        goto default;
                    }
                    mnemo = "STP";
                    break;
                case 0xdd:
                    mnemo = "CMP";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0xde:
                    mnemo = "DEC";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0xdf:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBS 5,";
                    args = AddrModeRelative();
                    break;

                case 0xe0:
                    mnemo = "CPX";
                    args = AddrModeImmediate();
                    break;
                case 0xe1:
                    mnemo = "SBC";
                    args = AddrModeZeroPageIndexedXIndirect();
                    break;
                case 0xe4:
                    mnemo = "CPX";
                    args = AddrModeZeroPage();
                    break;
                case 0xe5:
                    mnemo = "SBC";
                    args = AddrModeZeroPage();
                    break;
                case 0xe6:
                    mnemo = "INC";
                    args = AddrModeZeroPage();
                    break;
                case 0xe7:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "SMB 6,";
                    args = AddrModeZeroPage();
                    break;
                case 0xe8:
                    mnemo = "INX";
                    break;
                case 0xe9:
                    mnemo = "SBC";
                    args = AddrModeImmediate();
                    break;
                case 0xea:
                    mnemo = "NOP";
                    break;
                case 0xec:
                    mnemo = "CPX";
                    args = AddrModeAbsolute();
                    break;
                case 0xed:
                    mnemo = "SBC";
                    args = AddrModeAbsolute();
                    break;
                case 0xee:
                    mnemo = "INC";
                    args = AddrModeAbsolute();
                    break;
                case 0xef:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBS 6,";
                    args = AddrModeRelative();
                    break;

                case 0xf0:
                    mnemo = "BEQ";
                    args = AddrModeRelative();
                    break;
                case 0xf1:
                    mnemo = "SBC";
                    args = AddrModeZeroPageIndirectIndexedY();
                    break;
                case 0xf2:
                    // mode d'adressage absent du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "SBC";
                    args = AddrModeZeroPageIndirect();
                    break;
                case 0xf5:
                    mnemo = "SBC";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0xf6:
                    mnemo = "INC";
                    args = AddrModeZeroPageIndexedX();
                    break;
                case 0xf7:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "SMB 7,";
                    args = AddrModeZeroPage();
                    break;
                case 0xf8:
                    mnemo = "SED";
                    break;
                case 0xf9:
                    mnemo = "SBC";
                    args = AddrModeAbsoluteIndexedY();
                    break;
                case 0xfa:
                    // instruction absente du processeur d'origine
                    if (this.procLevel == ProcessorLevel.NMOS6502)
                    {
                        goto default;
                    }
                    mnemo = "PLX";
                    break;
                case 0xfd:
                    mnemo = "SBC";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0xfe:
                    mnemo = "DEC";
                    args = AddrModeAbsoluteIndexedX();
                    break;
                case 0xff:
                    // instruction ajoutée dans la version de Rockwell
                    if ( (this.procLevel == ProcessorLevel.NMOS6502) ||
                         (this.procLevel == ProcessorLevel.WDC65C02) )
                    {
                        goto default;
                    }
                    mnemo = "BBS 7,";
                    args = AddrModeRelative();
                    break;

                /* opcode inutilisé ! */
                default:
                    switch (this.uoPolicy) {
                        case UnknownOpcodePolicy.DoNop:
                            mnemo = "?!?";
                            break;
                        case UnknownOpcodePolicy.ThrowException:
                            throw new UnknownOpcodeException(
                                    this.regPC,
                                    opcode,
                                    String.Format(ERR_UNKNOWN_OPCODE,
                                                  this.regPC, opcode));
                        case UnknownOpcodePolicy.Emulate:
                            if (this.procLevel == ProcessorLevel.NMOS6502) {
                                ;
                                // TODO !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                            } else {
                                // undefined opcodes = NOPs on CMOS devices
                                mnemo = "?!?";
                            }
                            break;
                    }
                    break;
            }

            /* liste les octets ainsi traités */
            int nbOct = this.regPC - memoryAddress;
            for (int n = 0; n < nbOct; n++) {
                ushort ad = (ushort)(memoryAddress + n);
                byte b = ReadMem(ad);
                sbResult.Append(String.Format("{0:X2} ", b));
            }
            /* aligne le résultat sur colonnes */
            while (sbResult.Length < 16) sbResult.Append(" ");
            sbResult.Append(": ");

            /* enfin, liste l'instruction désassemblée */
            sbResult.Append(mnemo);
            sbResult.Append(' ');
            sbResult.Append(args);

            /* terminé */
            sbResult.Append(" \r\n");
            return sbResult.ToString();
        }

        /// <summary>
        /// Désassemble un nombre donné d'instructions en mémoire.
        /// </summary>
        /// <param name="fromAddress">
        /// Adresse mémoire de la première instruction à désassembler.
        /// </param>
        /// <param name="nbInstr">
        /// Nombre d'instructions consécutives à desassembler.
        /// </param>
        /// <returns>
        /// Chaîne de caractère contenant le désassemblage des instructions
        /// rencontrées à partir de <code>fromAddress</code>.
        /// </returns>
        /// <exception cref="AddressUnreadableException">
        /// Si l'une des adresses-mémoire à traiter est impossible à lire.
        /// </exception>
        public String DisassembleManyInstructionsAt(ushort fromAddress,
                                                    uint nbInstr)
        {
            StringBuilder sbResult = new StringBuilder();
            this.regPC = fromAddress;
            for (uint n = 0; n < nbInstr; n++) {
                string instr = DisassembleInstructionAt(
                        (ushort)(this.regPC));
                sbResult.Append(instr);
            }
            return sbResult.ToString();
        }

        /// <summary>
        /// Désassemble le contenu d'une plage d'adresses en mémoire.
        /// </summary>
        /// <param name="fromAddress">
        /// Adresse mémoire de la première instruction à désassembler.
        /// </param>
        /// <param name="toAddress">
        /// Dernière adresse mémoire à desassembler.
        /// </param>
        /// <returns>
        /// Chaîne de caractère contenant le désassemblage des adresses
        /// de la plage mémoire indiquée.
        /// <br/>
        /// Notez que le désassemblage peut aller légèrement au-delà de
        /// <code>toAddress</code> si une instruction s'étend sur cette
        /// adresse de fin.
        /// </returns>
        /// <exception cref="AddressUnreadableException">
        /// Si l'une des adresses-mémoire à traiter est impossible à lire.
        /// </exception>
        public String DisassembleMemory(ushort fromAddress,
                                        ushort toAddress)
        {
            StringBuilder sbResult = new StringBuilder();
            this.regPC = fromAddress;
            while (this.regPC <= toAddress) {
                string instr = DisassembleInstructionAt(
                        (ushort)(this.regPC));
                sbResult.Append(instr);
            }
            return sbResult.ToString();
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
        /// Niveau (type) de processeur dont le code binaire doit être
        /// désassemblé.
        /// </summary>
        public ProcessorLevel CPULevel
        {
            get { return this.procLevel; }
            set { this.procLevel = value; }
        }

        /// <summary>
        /// Politique de prise en charge des opcodes invalides
        /// au désassemblage.
        /// </summary>
        public UnknownOpcodePolicy InvalidOpcodePolicy
        {
            get { return this.uoPolicy; }
            set { this.uoPolicy = value; }
        }

    }
}


