﻿using System;
using System.Text;


namespace GUIEmu6502
{
    /// <summary>
    /// Classe représentant une condition d'arrêt pour
    /// le déboguage sur un processeur virtuel 65x02.
    /// </summary>
    public partial class DebuggerTrap6502
    {
        /* =========================== CONSTANTES =========================== */

        // messages d'erreur
        private const String UNKNOWN_KIND_STRING =
                "Type de point d'arrêt inconnu (\"{0}\") !";
        private const String BAD_TRAP_STRING_FORMAT =
                "Chaîne de point d'arrêt invalide (\"{0}\") !";

        // chaînes liées au format (NE PAS TRADUIRE !)
        private const string TRAP_KIND_BREAKPOINT = "BKPT_PC";
        private const string TRAP_STACK_UNDERFLOW = "S_UNDER";
        private const string TRAP_KIND_A_EQUALS = "A_EQUAL";
        private const string TRAP_KIND_A_LESS_THAN = "A_LESS_";
        private const string TRAP_KIND_A_MORE_THAN = "A_MORE_";
        private const string TRAP_KIND_X_EQUALS = "X_EQUAL";
        private const string TRAP_KIND_X_LESS_THAN = "X_LESS_";
        private const string TRAP_KIND_X_MORE_THAN = "X_MORE_";
        private const string TRAP_KIND_Y_EQUALS = "Y_EQUAL";
        private const string TRAP_KIND_Y_LESS_THAN = "Y_LESS_";
        private const string TRAP_KIND_Y_MORE_THAN = "Y_MORE_";


        /* ========================== CHAMPS PRIVÉS ========================= */

        private bool active;

        private DebuggerTrapKind6502 kind;

        private int val;


        /* ======================= MÉTHODES PUBLIQUES ======================= */

        /* ~~ Statiques ~~ */

        /// <summary>
        /// Renvoie le code textuel correspondant à un type de condition
        /// d'arrêt.
        /// </summary>
        /// <param name="kind">
        /// Type de condition d'arrêt dont on veut le code textuel.
        /// </param>
        /// <returns>
        /// Code textuel correspondant à <code>kind</code>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Si <code>kind</code> n'est pas reconnu.
        /// </exception>
        public static String TrapKindToString(DebuggerTrapKind6502 kind)
        {
            switch (kind) {
                case DebuggerTrapKind6502.Breakpoint:
                    return TRAP_KIND_BREAKPOINT;
                case DebuggerTrapKind6502.SPunderflow:
                    return TRAP_STACK_UNDERFLOW;
                case DebuggerTrapKind6502.Aequals:
                    return TRAP_KIND_A_EQUALS;
                case DebuggerTrapKind6502.AlessThan:
                    return TRAP_KIND_A_LESS_THAN;
                case DebuggerTrapKind6502.AmoreThan:
                    return TRAP_KIND_A_MORE_THAN;
                case DebuggerTrapKind6502.Xequals:
                    return TRAP_KIND_X_EQUALS;
                case DebuggerTrapKind6502.XlessThan:
                    return TRAP_KIND_X_LESS_THAN;
                case DebuggerTrapKind6502.XmoreThan:
                    return TRAP_KIND_X_MORE_THAN;
                case DebuggerTrapKind6502.Yequals:
                    return TRAP_KIND_Y_EQUALS;
                case DebuggerTrapKind6502.YlessThan:
                    return TRAP_KIND_Y_LESS_THAN;
                case DebuggerTrapKind6502.YmoreThan:
                    return TRAP_KIND_Y_MORE_THAN;
                default:
                    throw new ArgumentOutOfRangeException("kind");
            }
        }

        /// <summary>
        /// Retrouve un type de condition d'arrêt depuis un code textuel.
        /// </summary>
        /// <param name="sk">
        /// Code textuel représentant le type de condition d'arrêt à créer.
        /// </param>
        /// <returns>
        /// Type de condition d'arrêt correspondant à <code>sk</code>.
        /// </returns>
        /// <exception cref="FormatException">
        /// Si <code>sk</code> ne correpond à aucun type connu.
        /// </exception>
        public static DebuggerTrapKind6502 TrapKindFromString(string sk)
        {
            switch (sk.Trim().ToUpper()) {
                case TRAP_KIND_BREAKPOINT:
                    return DebuggerTrapKind6502.Breakpoint;
                case TRAP_STACK_UNDERFLOW:
                    return DebuggerTrapKind6502.SPunderflow;
                case TRAP_KIND_A_EQUALS:
                    return DebuggerTrapKind6502.Aequals;
                case TRAP_KIND_A_LESS_THAN:
                    return DebuggerTrapKind6502.AlessThan;
                case TRAP_KIND_A_MORE_THAN:
                    return DebuggerTrapKind6502.AmoreThan;
                case TRAP_KIND_X_EQUALS:
                    return DebuggerTrapKind6502.Xequals;
                case TRAP_KIND_X_LESS_THAN:
                    return DebuggerTrapKind6502.XlessThan;
                case TRAP_KIND_X_MORE_THAN:
                    return DebuggerTrapKind6502.XmoreThan;
                case TRAP_KIND_Y_EQUALS:
                    return DebuggerTrapKind6502.Yequals;
                case TRAP_KIND_Y_LESS_THAN:
                    return DebuggerTrapKind6502.YlessThan;
                case TRAP_KIND_Y_MORE_THAN:
                    return DebuggerTrapKind6502.YmoreThan;
                default:
                    throw new FormatException(String.Format(
                            UNKNOWN_KIND_STRING,
                            sk));
            }
        }

        /// <summary>
        /// Créé une condition d'arrêt depuis sa représentation textuelle.
        /// </summary>
        /// <param name="st">
        /// Chaîne contenant la représentation textuelle d'une condition
        /// d'arrêt.
        /// </param>
        /// <returns>
        /// Condition d'arrêt correspondant à <code>st</code>.
        /// </returns>
        /// <seealso cref="DebuggerTrap6502.ToString"/>
        /// <exception cref="FormatException">
        /// Si <code>st</code> n'est pas au format correct.
        /// </exception>
        public static DebuggerTrap6502 FromString(string st)
        {
            st = st.TrimEnd().ToUpper();
            if (st.Length < 9)
                throw new FormatException(String.Format(
                        BAD_TRAP_STRING_FORMAT,
                        st));
            DebuggerTrap6502 dt = new DebuggerTrap6502 {
                active = (st[0] == 'A'),
                kind = TrapKindFromString(st.Substring(2, 7))
            };
            switch (dt.kind) {
                case DebuggerTrapKind6502.Breakpoint:
                    if (st.Length < 14)
                        throw new FormatException(String.Format(
                                BAD_TRAP_STRING_FORMAT,
                                st));
                    dt.val = Convert.ToInt16(st.Substring(10, 4), 16);
                    break;
                case DebuggerTrapKind6502.Aequals:
                case DebuggerTrapKind6502.AlessThan:
                case DebuggerTrapKind6502.AmoreThan:
                case DebuggerTrapKind6502.Xequals:
                case DebuggerTrapKind6502.XlessThan:
                case DebuggerTrapKind6502.XmoreThan:
                case DebuggerTrapKind6502.Yequals:
                case DebuggerTrapKind6502.YlessThan:
                case DebuggerTrapKind6502.YmoreThan:
                    if (st.Length < 12)
                        throw new FormatException(String.Format(
                                BAD_TRAP_STRING_FORMAT,
                                st));
                    dt.val = Convert.ToByte(st.Substring(10, 2), 16);
                    break;
            }
            return dt;
        }

        /* ~~ Héritée ~~ */

        /// <returns>
        /// Représentation textuelle de la présente condition d'arrêt.
        /// </returns>
        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this.active ? 'A' : ' ');
            sb.Append(':');
            sb.Append(TrapKindToString(this.kind));
            if (this.kind != DebuggerTrapKind6502.SPunderflow) {
                sb.Append(':');
                sb.Append(this.HexValue);
            }
            return sb.ToString();
        }


        /* ====================== PROPRIÉTÉS PUBLIQUES ====================== */

        /// <summary>
        /// Indique si le point d'arrêt est actif ou non.
        /// </summary>
        public Boolean Enabled
        {
            get { return this.active; }
            set { this.active = value; }
        }

        /// <summary>
        /// Type du point d'arrêt.
        /// <see cref="DebuggerTrapKind6502"/>
        /// </summary>
        public DebuggerTrapKind6502 TrapKind
        {
            get { return this.kind; }
            set { this.kind = value; }
        }

        /// <summary>
        /// Valeur associée au point d'arrêt :
        /// <ul>
        /// <li> Adresse pour un point d'arrêt PC.</li>
        /// <li> Octet pour une valeur du registre A, X ou Y.</li>
        /// </ul>
        /// </summary>
        public int ReferenceValue
        {
            get { return this.val; }
            set { this.val = value; }
        }

        /// <summary>
        /// Valeur associée au point d'arrêt, sous forme hexadécimale.
        /// </summary>
        public string HexValue
        {
            get {
                switch (this.kind) {
                    case DebuggerTrapKind6502.Breakpoint:
                        return this.val.ToString("X4");
                    case DebuggerTrapKind6502.Aequals:
                    case DebuggerTrapKind6502.AlessThan:
                    case DebuggerTrapKind6502.AmoreThan:
                    case DebuggerTrapKind6502.Xequals:
                    case DebuggerTrapKind6502.XlessThan:
                    case DebuggerTrapKind6502.XmoreThan:
                    case DebuggerTrapKind6502.Yequals:
                    case DebuggerTrapKind6502.YlessThan:
                    case DebuggerTrapKind6502.YmoreThan:
                        return this.val.ToString("X2");
                }
                return null;
            }
            set {
                switch (this.kind) {
                    case DebuggerTrapKind6502.Breakpoint:
                        this.val = Convert.ToUInt16(value, 16);
                        break;
                    case DebuggerTrapKind6502.Aequals:
                    case DebuggerTrapKind6502.AlessThan:
                    case DebuggerTrapKind6502.AmoreThan:
                    case DebuggerTrapKind6502.Xequals:
                    case DebuggerTrapKind6502.XlessThan:
                    case DebuggerTrapKind6502.XmoreThan:
                    case DebuggerTrapKind6502.Yequals:
                    case DebuggerTrapKind6502.YlessThan:
                    case DebuggerTrapKind6502.YmoreThan:
                        this.val = Convert.ToByte(value, 16);
                        break;
                    default:
                        this.val = 0;
                        break;
                }
            }
        }


    }

}

