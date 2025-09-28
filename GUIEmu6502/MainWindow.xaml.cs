using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Win32;

using Emulator6502;


namespace GUIEmu6502
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /* =========================== CONSTANTES =========================== */

        // messages affichés
        const string WARN_TITLE = "Attention !";
        const string WARN_REINIT_EMULATOR =
                "Voulez-vous vraiment réinitialiser l'émulateur ?\r\n" +
                "Tout le travail en cours sera perdu !";
        const string WARN_QUIT_EMULATOR =
                "Voulez-vous vraiment quitter l'émulateur ?\r\n" +
                "Tout le travail en cours sera perdu !";


        /* ========================== CHAMPS PRIVÉS ========================= */

        // "flag" signalant que l'interface est contruite
        private bool guiDone = false;

        // processeur émulé
        private CPU6502 processor;

        // espace-mémoire attaché au processeur
        // (défini une fois pour toutes à la construction)
        private BasicMemorySpace6502 memSpace;

        // outil de désassemblage
        private Disasm6502 disasm;

        // outil de formatage de la mémoire
        private MemoryFormatter_8bit memFmt;

        // outil de formatage de la pile
        private StackFormatter6502 stkFmt;


        /* ========================== CONSTRUCTEUR ========================== */

        /// <summary>
        /// Constructeur par défaut (et unique) de cette classe.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }


        /* ======================== MÉTHODES PRIVÉES ======================== */

        /* ~~ Méthodes utilitaires ~~ */

        private static byte HexToByte(string hex)
        {
            return Convert.ToByte(hex, 16);
        }

        private static ushort HexToAddr(string hex)
        {
            return Convert.ToUInt16(hex, 16);
        }

        /* ~~ Gestion des contrôles ~~ */

        private void UpdateRegisterView()
        {
            this.tbRegA.Text = String.Format("{0:X2}",
                                             this.processor.RegisterA);
            this.tbRegX.Text = String.Format("{0:X2}",
                                             this.processor.RegisterX);
            this.tbRegY.Text = String.Format("{0:X2}",
                                             this.processor.RegisterY);
            this.tbRegS.Text = String.Format("{0:X2}",
                                             this.processor.RegisterS);
            this.tbRegPC.Text = String.Format("{0:X4}",
                                              this.processor.RegisterPC);
            this.tbRegP.Text = String.Format("{0:X2}",
                                             this.processor.RegisterP);
            this.cbFlagN.IsChecked = this.processor.FlagN;
            this.cbFlagV.IsChecked = this.processor.FlagV;
            this.cbFlagRsvd.IsChecked = true;
            this.cbFlagB.IsChecked = this.processor.FlagB;
            this.cbFlagD.IsChecked = this.processor.FlagD;
            this.cbFlagI.IsChecked = this.processor.FlagI;
            this.cbFlagZ.IsChecked = this.processor.FlagZ;
            this.cbFlagC.IsChecked = this.processor.FlagC;
        }

        private void UpdateStackView()
        {
            string stackView =
                    this.stkFmt.ListStackValues(this.processor.RegisterS);
            this.tbStackView.Text = stackView;
        }

        private void UpdateMemoryView()
        {
            this.Cursor = Cursors.Wait;
            try {
                ushort from = HexToAddr(this.tbMemoryFrom.Text);
                ushort to = HexToAddr(this.tbMemoryTo.Text);
                string memView = this.memFmt.ListMemoryValues(from, to);
                this.tbMemoryView.Text = memView;
            } finally {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void UpdateDisasmView()
        {
            this.Cursor = Cursors.Wait;
            try {
                ushort from = HexToAddr(this.tbDisasmFrom.Text);
                ushort to = HexToAddr(this.tbDisasmTo.Text);
                string disasm = this.disasm.DisassembleMemory(from, to);
                this.tbDisasmView.Text = disasm;
            } finally {
                this.Cursor = Cursors.Arrow;
            }
        }

        /* ~~ Gestionnaires d'évènements ~~ */

        /* gère l'ouverture de la fenêtre = lancement du programme */
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            /* créé l'espace-mémoire émulé */
            this.memSpace = new BasicMemorySpace6502();
            /* créé le processeur émulé */
            this.processor = new CPU6502(this.memSpace);
            this.processor.Reset();

            /* outils / utilitaires */
            this.disasm = new Disasm6502(this.memSpace);
            this.memFmt = new MemoryFormatter_8bit(this.memSpace);
            this.stkFmt = new StackFormatter6502(this.memSpace);

            /* initialise les contrôles */
            this.tbDisasmView.Text = String.Empty;
            this.tbMemoryView.Text = String.Empty;
            this.tbStackView.Text = String.Empty;

            /* prêt à traiter les évènements */
            guiDone = true;

            /* met à jour le contenu des fenêtres */
            UpdateRegisterView();
            UpdateStackView();
            UpdateMemoryView();
            UpdateDisasmView();
        }


        private void TbDisasmLimits_TextChanged(object sender,
                                                TextChangedEventArgs e)
        {
            if (!guiDone) return;
            /* régénère le désassemblage */
            UpdateDisasmView();
        }

        private void TbMemoryLimits_TextChanged(object sender,
                                                TextChangedEventArgs e)
        {
            if (!guiDone) return;
            /* régénère la vue de la mémoire */
            UpdateMemoryView();
        }


        private void TbRegA_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du registre A (accumulateur) */
            this.processor.RegisterA = HexToByte(this.tbRegA.Text);
            UpdateRegisterView();
        }

        private void TbRegX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du registre X */
            this.processor.RegisterX = HexToByte(this.tbRegX.Text);
            UpdateRegisterView();
        }

        private void TbRegY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du registre Y */
            this.processor.RegisterY = HexToByte(this.tbRegY.Text);
            UpdateRegisterView();
        }

        private void TbRegS_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du registre S (pointeur de pile) */
            this.processor.RegisterS = HexToByte(this.tbRegS.Text);
            UpdateRegisterView();
            /* MàJ de la vue de la pile */
            UpdateStackView();
        }

        private void TbRegPC_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du registre A (accumulateur) */
            this.processor.RegisterPC = HexToAddr(this.tbRegPC.Text);
            UpdateRegisterView();
        }

        private void CbFlagN_Checked(object sender, RoutedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du flag N (Négatif) */
            this.processor.FlagN = this.cbFlagN.IsChecked.Value;
            UpdateRegisterView();
        }

        private void CbFlagV_Checked(object sender, RoutedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du flag V ("oVerflow") */
            this.processor.FlagV = this.cbFlagV.IsChecked.Value;
            UpdateRegisterView();
        }

        private void CbFlagB_Checked(object sender, RoutedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du flag B ("Break") */
            this.processor.FlagB = this.cbFlagB.IsChecked.Value;
            UpdateRegisterView();
        }

        private void CbFlagD_Checked(object sender, RoutedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du flag D (Décimal) */
            this.processor.FlagD = this.cbFlagD.IsChecked.Value;
            UpdateRegisterView();
        }

        private void CbFlagI_Checked(object sender, RoutedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du flag I (Interruptions masquées) */
            this.processor.FlagI = this.cbFlagI.IsChecked.Value;
            UpdateRegisterView();
        }

        private void CbFlagZ_Checked(object sender, RoutedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du flag Z (Zéro) */
            this.processor.FlagZ = this.cbFlagZ.IsChecked.Value;
            UpdateRegisterView();
        }

        private void CbFlagC_Checked(object sender, RoutedEventArgs e)
        {
            if (!guiDone) return;
            /* change la valeur du flag C ("Carry") */
            this.processor.FlagC = this.cbFlagC.IsChecked.Value;
            UpdateRegisterView();
        }


        private void MenuFileNew_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult res = MessageBox.Show(
                    this,
                    WARN_REINIT_EMULATOR,
                    WARN_TITLE,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
            if (res != MessageBoxResult.Yes) return;
            /* réinitialise l'espace mémoire et le CPU */
            this.memSpace.Clear();
            this.processor.Reset();
        }

        private void MenuFileOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.AddExtension = true;
            ofd.CheckFileExists = true;
            ofd.CheckPathExists = true;
            ofd.DefaultExt = "bin";
            ofd.Filter = "Fichiers binaires|*.bin|Tous les fichiers|*.*";
            ofd.Multiselect = false;
            ofd.Title = "Sélectionnez le fichier binaire à charger";
            ofd.ValidateNames = true;
            if (ofd.ShowDialog() != true) return;
            string srcFilePath = ofd.FileName;
            this.memSpace.LoadFromFile(srcFilePath);
            /* réinit le processeur */
            this.processor.Reset();
            /* MàJ de l'interface */
            UpdateRegisterView();
            UpdateStackView();
            UpdateMemoryView();
            UpdateDisasmView();
        }

        private void MenuFileSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.AddExtension = true;
            sfd.CheckFileExists = false;
            sfd.CheckPathExists = true;
            sfd.DefaultExt = "bin";
            sfd.Filter = "Fichiers binaires|*.bin|Tous les fichiers|*.*";
            sfd.Title = "Sélectionnez le fichier binaire à sauvegarder";
            sfd.ValidateNames = true;
            if (sfd.ShowDialog() != true) return;
            string destFilePath = sfd.FileName;
            this.memSpace.SaveToFile(destFilePath);
        }

        private void MenuFileQuit_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult res = MessageBox.Show(
                    this,
                    WARN_QUIT_EMULATOR,
                    WARN_TITLE,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
            if (res != MessageBoxResult.Yes) return;
            /* quitte le programme en fermant cette fenêtre */
            Close();
        }

        private void MenuSimuStep_Click(object sender, RoutedEventArgs e)
        {
            this.processor.Step();
            /* MàJ de l'interface */
            UpdateRegisterView();
            UpdateStackView();
            UpdateMemoryView();
            // UpdateDisasmView();
        }



        /* ======================= MÉTHODES PUBLIQUES ======================= */

    }
}

