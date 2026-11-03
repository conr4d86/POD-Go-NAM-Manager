using System;using System.Windows.Forms;

sealed class InstallNotice {
 public string Title,Text;public MessageBoxIcon Icon;
 public static InstallNotice For(FirmwareFile file){
  if(file==null)throw new ArgumentNullException("file");
  if(file.IsOfficial)return new InstallNotice{Title="Restaurar firmware oficial",Icon=MessageBoxIcon.Information,Text="Al volver al firmware oficial, carga tu backup desde POD Go Edit para recuperar tus presets anteriores.\n\nPulsa OK para comenzar la restauración."};
  return new InstallNotice{Title="Antes de instalar CF NAM Conr4d",Icon=MessageBoxIcon.Warning,Text="Por precaución, realiza un backup con POD Go Edit antes de instalar el custom firmware.\n\nConserva esa copia para poder recuperar tus presets.\n\nPulsa OK cuando tengas tu backup y quieras comenzar la instalación."};
 }
 public bool Confirm(IWin32Window owner){return MessageBox.Show(owner,Text,Title,MessageBoxButtons.OKCancel,Icon,MessageBoxDefaultButton.Button2)==DialogResult.OK;}
}
