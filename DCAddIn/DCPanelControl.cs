﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using DevExpress.XtraGrid.Views.Base;
using System.Text.RegularExpressions;
using System.Linq.Dynamic;
using System.Threading.Tasks;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using System.Configuration;
using System.ServiceModel.Security;

namespace DCAddIn
{


    public partial class DCPanelControl : UserControl
    {
        
        private delegate void ToDoDelegate();

        private int lastRow = 1;

        public bool enProceso = false;

        public bool enProcesoExchange = false;

        public bool enProcesoArchivos = false;

        public DCPanelControl()
        {
            InitializeComponent();
            itemsGridControl.Width=this.Width-95;
            itemsGridControl.Height = this.Height - 5;
            dataSource = columnasDataTable;
            itemsGridControl.DataSource = dataSource;
        }

        private DataTable exchangeTabla;

        private DataTable archivosTabla;

        private DataTable dataSource;

        public DataTable DataSource
        {
            get
            {
                return dataSource;
            }
            private set
            {
                dataSource = value;
            }
        }

        public GridView ItemsGridView
        {
            get
            {
                return itemsGridView;
            }
            private set
            {
                itemsGridView = value;
            }
        }

        private DataTable columnasDataTable
        {
            get
            {
                DataTable dt = new DataTable("columnas");
                dt.Columns.Add("ProcesoID", typeof(short));
                dt.Columns.Add("Nombre", typeof(String));
                dt.Columns.Add("Nota", typeof(String));
                dt.Columns.Add("File", typeof(String));
                dt.Columns.Add("NombreFile", typeof(String));
                dt.Columns.Add("Mercado", typeof(String));
                dt.Columns.Add("TipoID", typeof(short));
                dt.Columns.Add("Carpeta", typeof(String));
                dt.Columns.Add("CuentaID", typeof(String));
                dt.Columns.Add("Identificador", typeof(String));
                dt.Columns.Add("ID", typeof(Int32));
                dt.Columns.Add("Ano", typeof(String));
                dt.Columns.Add("NumFile", typeof(String));
                dt.Columns.Add("NumFileFisico", typeof(String));
                dt.Columns.Add("FechaHora", typeof(DateTime));
                dt.Columns.Add("Sender", typeof(string));
                dt.Columns.Add("Reciever", typeof(string));
                return dt;
            }
        }

        private void ProcesarTabla()
        {
            itemsGridView.CloseEditor();
            itemsGridView.UpdateCurrentRow();

            DataRow[] matches = dataSource.Select("NombreFile is null");

            if (matches.Length > 0)
            {
                XtraMessageBox.Show("No se ingresó el numero de file o la información es incorrecta.", "Datos no completos", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            matches = dataSource.Select("TipoID is null");

            if (matches.Length > 0)
            {
                XtraMessageBox.Show("No se seleccionó el tipo o la información es incorrecta.", "Datos no completos", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (enProceso == true)
            {
                XtraMessageBox.Show("Los datos se estan procesando, por favor espere...");
                return;
            }

            enProceso = true;

            itemsGridView.OptionsBehavior.Editable = false;

            DataRow[] correos = dataSource.Select("ProcesoID=3");
            exchangeTabla = dataSource.Clone();

            foreach (DataRow row in correos)
            {
                exchangeTabla.Rows.Add(row.ItemArray);
            }

            DataRow[] archivos = dataSource.Select("ProcesoID=1 or ProcesoID=2");
            archivosTabla = dataSource.Clone();

            foreach (DataRow row in archivos)
            {
                archivosTabla.Rows.Add(row.ItemArray);
            }

            BackgroundWorker tareaCorreos = new BackgroundWorker();
            tareaCorreos.DoWork += ProcesarTablaExchange;
            tareaCorreos.RunWorkerCompleted += ProcesoTerminadoExchange;
            tareaCorreos.RunWorkerAsync();

            BackgroundWorker tareaArchivos = new BackgroundWorker();
            tareaArchivos.DoWork += ProcesarTablaArchivos;
            tareaArchivos.RunWorkerCompleted += ProcesoTerminadoArchivos;
            tareaArchivos.RunWorkerAsync();
        }

        private void ProcesoTerminadoArchivos(object sender, RunWorkerCompletedEventArgs e)
        {
            enProcesoArchivos = false;
            if (enProcesoExchange == false)
            {
                XtraMessageBox.Show("El proceso ha concluido, las filas correctamente procesadas fueron removidas de la lista.");
                Invoke(new ToDoDelegate(() => itemsGridView.OptionsBehavior.Editable = true));
                enProceso = false;
            }
        }

        private void ProcesarTablaArchivos(object sender, DoWorkEventArgs e)
        {
            enProcesoArchivos = true;
            
            string servidorIP = Globals.ThisAddIn.CurrentServer;

            if (servidorIP != "")
            {
                string rutaBase = "\\\\" + servidorIP + "\\" + ConfigurationManager.AppSettings["depositoCarpeta"].ToString();

                try
                {
                    archivosTabla.Columns.Add("Archivo", typeof(string));
                    archivosTabla.Columns.Add("FechaHoraProceso", typeof(DateTime));

                    foreach (DataRow fila in archivosTabla.Rows)
                    {
                        DirectoryInfo directoryInfo = new DirectoryInfo(fila["Identificador"].ToString());

                        DateTime fechaHoraProceso = DateTime.Now;
                        
                        string anoPath = fechaHoraProceso.ToString("yyyy");
                        string mesPath = fechaHoraProceso.ToString("MM");
                        string diaPath = fechaHoraProceso.ToString("dd");

                        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                        var random = new Random();
                        string randomString = new string(
                        Enumerable.Repeat(chars, 70)
                        .Select(s => s[random.Next(s.Length)])
                        .ToArray()
                        );

                        string nombreArchivo = anoPath + "-" + mesPath + "-" + diaPath + "_" + randomString + directoryInfo.Extension;

                        
                        fila["Identificador"] = "<" + Path.GetFileNameWithoutExtension(directoryInfo.Name) + ">";
                        
   
                        fila["Archivo"] = nombreArchivo;
                        fila["FechaHoraProceso"] = fechaHoraProceso;
                        File.Copy(directoryInfo.FullName, rutaBase + "\\" + nombreArchivo);
                    }

                    try
                    {
                        DCServicio.DCServicioClient cliente = Globals.ThisAddIn.ClienteServicio;

                        List<int> resultado = cliente.ProcesarArchivos(archivosTabla);
                        cliente.Close();

                        Invoke(new ToDoDelegate(() =>
                        {

                            for (int x = 0; x < resultado.Count(); x++)
                            {
                                DataRow[] matches = dataSource.Select("ID='" + resultado[x] + "'");
                                foreach (DataRow row in matches)
                                {
                                    dataSource.Rows.Remove(row);
                                }
                            }
                            dataSource.AcceptChanges();
                        }));
                    }
                    catch
                    {
                        XtraMessageBox.Show("El archivo no se encontro, no mueva o elimine el archivo antes que se realice el proceso.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch
                {
                   XtraMessageBox.Show("Hubo un error en el proceso del archivo.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                if (archivosTabla.Rows.Count > 0)
                {
                    MessageBox.Show("Solo esta implementada la carga en red local");
                }
            }
        }

        private void ProcesoTerminadoExchange(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            enProcesoExchange = false;
            if (enProcesoArchivos == false)
            {
                XtraMessageBox.Show("El proceso ha concluido, las filas correctamente procesadas fueron removidas de la lista.");
                Invoke(new ToDoDelegate(() => itemsGridView.OptionsBehavior.Editable = true));
                enProceso = false;
            }
            
        }

        private void ProcesarTablaExchange(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            enProcesoExchange = true;
            
            List<string> columnas = new List<string>() { "CuentaID" };

            List<DataTable> datos = SplitTable(exchangeTabla, columnas);

            try {
                DCServicio.DCServicioClient cliente = Globals.ThisAddIn.ClienteServicio;
                List<int> resultado = cliente.ProcesarCorreos(datos);
                cliente.Close();

                Invoke(new ToDoDelegate(() => {

                    for (int x = 0; x < resultado.Count(); x++)
                    {
                        DataRow[] matches = dataSource.Select("ID='" + resultado[x] + "'");
                        foreach (DataRow row in matches)
                        {
                            dataSource.Rows.Remove(row);
                        }
                    }
                    dataSource.AcceptChanges();
                }));
  
            }
            catch
            {
                XtraMessageBox.Show("Hubo un error en el proceso del correo.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void DCPanelControl_Load(object sender, EventArgs e)
        {

            DCServicio.DCServicioClient cliente = Globals.ThisAddIn.ClienteServicio;
           
            List<DCServicio.Tipo> tipos = cliente.GetTipos();
            
            RepositoryItemLookUpEdit tipoLookup = new RepositoryItemLookUpEdit();
            tipoLookup.DropDownRows = 3;
            tipoLookup.DisplayMember = "Nombre";
            tipoLookup.ValueMember = "TipoID";
            tipoLookup.NullText = "Selecione...";
            tipoLookup.DataSource = tipos;

            List<DCServicio.Proceso> procesos = cliente.GetProcesos();

            RepositoryItemLookUpEdit procesoLookup = new RepositoryItemLookUpEdit();
            procesoLookup.DropDownRows = 3;
            procesoLookup.DisplayMember = "Nombre";
            procesoLookup.ValueMember = "ProcesoID";
            procesoLookup.NullText = "Selecione...";
            procesoLookup.DataSource = procesos;

            itemsGridView.OptionsView.ShowGroupPanel = false;
            itemsGridView.OptionsNavigation.EnterMoveNextColumn = true;
            itemsGridView.Columns["ProcesoID"].Width = 20;
            itemsGridView.Columns["ProcesoID"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["ProcesoID"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["ProcesoID"].ColumnEdit = procesoLookup;
            itemsGridView.Columns["ProcesoID"].Caption = "Proceso";
            itemsGridView.Columns["File"].Width=25;
            itemsGridView.Columns["Mercado"].Width = 35;
            itemsGridView.Columns["Mercado"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["Mercado"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["NombreFile"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["NombreFile"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["TipoID"].Width = 25;
            itemsGridView.Columns["TipoID"].ColumnEdit = tipoLookup;
            itemsGridView.Columns["TipoID"].Caption = "Tipo";
            itemsGridView.Columns["Carpeta"].Width = 50;
            itemsGridView.Columns["Carpeta"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["Carpeta"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["CuentaID"].Width = 50;
            itemsGridView.Columns["CuentaID"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["CuentaID"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["CuentaID"].Caption = "Cuenta";
            itemsGridView.Columns["Identificador"].Visible = false;
            itemsGridView.Columns["Identificador"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["Identificador"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["ID"].Visible = false;
            itemsGridView.Columns["ID"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["ID"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["Ano"].Width = 15;
            itemsGridView.Columns["Ano"].Visible = false;
            itemsGridView.Columns["Ano"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["Ano"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["Ano"].Caption = "Año";
            itemsGridView.Columns["NumFile"].Width = 25;
            itemsGridView.Columns["NumFile"].Visible = false;
            itemsGridView.Columns["NumFile"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["NumFile"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["NumFile"].Caption = "# Cotización";
            itemsGridView.Columns["NumFileFisico"].Width = 25;
            itemsGridView.Columns["NumFileFisico"].Visible = false;
            itemsGridView.Columns["NumFileFisico"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["NumFileFisico"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["NumFileFisico"].Caption = "# File";
            itemsGridView.Columns["FechaHora"].Width = 25;
            itemsGridView.Columns["FechaHora"].Visible = false;
            itemsGridView.Columns["FechaHora"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["FechaHora"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["FechaHora"].Caption = "Fecha";
            itemsGridView.Columns["Sender"].Width = 25;
            itemsGridView.Columns["Sender"].Visible = false;
            itemsGridView.Columns["Sender"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["Sender"].OptionsColumn.AllowFocus = false;
            itemsGridView.Columns["Reciever"].Width = 25;
            itemsGridView.Columns["Reciever"].Visible = false;
            itemsGridView.Columns["Reciever"].OptionsColumn.AllowEdit = false;
            itemsGridView.Columns["Reciever"].OptionsColumn.AllowFocus = false;

            cliente.Close();
        }

        private void DCPanelControl_Resize(object sender, EventArgs e)
        {
            itemsGridControl.Width = this.Width - 95;
            itemsGridControl.Height = this.Height - 5;
        }

        public void ItemsGridControl_AgregarFilaPanel(List<object> datos)
        {
            if (enProceso == false)
            {
                itemsGridView.CloseEditor();
                itemsGridView.UpdateCurrentRow();
                itemsGridView.AddNewRow();
                int newRowHandle = itemsGridView.FocusedRowHandle;

                itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["ProcesoID"], datos[0]);
                itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["Nombre"], datos[1]);
                itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["Carpeta"], datos[2]);
                itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["CuentaID"], datos[3]);
                itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["Identificador"], datos[4]);
                itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["Sender"], datos[5]);
                itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["Reciever"], datos[6]);
                itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["FechaHora"], datos[7]);
                itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["ID"], lastRow);

                string numFile = ObtenerFileAsunto(datos[1].ToString());
                if (numFile != null)
                {
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["File"], numFile);
                }

                lastRow++;
            }
            else
            {
                XtraMessageBox.Show("Se esta procesando el ultimo envio, por favor espere..");
            }
        }
 
        public List<DataTable> SplitTable(DataTable dt, List<string> grupos)
        {

            List<DataTable> resultado = new List<DataTable>();
            //List<string> fields = new List<string>() { "Supplier", "Country" };
            string gFields = string.Join(", ", grupos.Select(x => "it[\"" + x + "\"] as " + x));
            // qfields = "it[\"Supplier\"] as Supplier, it[\"Country\"] as Country"

            var q = dt
                .AsEnumerable()
                .AsQueryable()
                .GroupBy("new(" + gFields + ")", "it")
                .Select("new (it as Data)");
            foreach (dynamic d in q)
            {
                var dtemp = dt.Clone();

                foreach (var row in d.Data)
                    dtemp.Rows.Add(row.ItemArray);

                resultado.Add(dtemp);
            }
            return resultado;
        }

        private string ObtenerFileAsunto(string cadena)
        {

            MatchCollection matchCollection = Regex.Matches(cadena, @"(\d*-\d*)",
                RegexOptions.IgnoreCase);
            if (matchCollection.Count > 0)
            {
                foreach (Match match in matchCollection)
                {
                    string matchFile = FormatoFile(match.Groups[1].Value);

                    if (!String.IsNullOrEmpty(matchFile))
                    {
                        
                        return matchFile;
                    }
                }
            }

            return null;
        }

        private void ItemsGridView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            DCServicio.DCServicioClient cliente = Globals.ThisAddIn.ClienteServicio;
            
            if (e.Column.FieldName == "File")
            {
                int newRowHandle = itemsGridView.FocusedRowHandle;

                string anoFile = FormatoFile(e.Value.ToString(), 5);

                if (anoFile == null)
                {
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["NombreFile"], null);
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["Mercado"], null);
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["Ano"], null);
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["NumFile"], null);
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["NumFileFisico"], null);
                    return;
                }
                else
                {
                    DCServicio.FilePrincipal datos = cliente.GetFilePrincipalByAnoFile(anoFile);
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["NombreFile"], datos.Nombre);
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["Mercado"], datos.Mercado);
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["Ano"], datos.Ano);
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["NumFile"], datos.NumFile);
                    itemsGridView.SetRowCellValue(newRowHandle, itemsGridView.Columns["NumFileFisico"], datos.NumFileFisico);
                }

                /*
                    string oradb = "DATA SOURCE=vipa;USER ID=usuario;Password=xxxx;";
                    OracleConnection conn = new OracleConnection(oradb);
                    conn.Open();
                    OracleCommand cmd = new OracleCommand();
                    cmd.Connection = conn;
                    cmd.CommandText = "Select nom_file, ano, num_file, num_file_fisico from file_principal where ano = :ano and num_file_fisico = :num_file_fisico ";
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add("num_file_fisico", OracleDbType.Varchar2, ParameterDirection.Input).Value = partes[0];
                    cmd.Parameters.Add("ano", OracleDbType.Varchar2, ParameterDirection.Input).Value = partes[1];
                    OracleDataReader dr = cmd.ExecuteReader();
                    dr.Read();
                    conn.Dispose();
                */
            }

            cliente.Close();

        }

        public string FormatoFile(string file, int longitud = 5)
        {

            char[] separadores = new char[1];

            if (file.IndexOf("-") != -1)
            {
                separadores[0] = '-';
            }
            else if (file.IndexOf(".") != -1)
            {
                separadores[0] = '.';
            }
            else
            {
                return null;
            }

            string[] partes = file.Split(separadores, StringSplitOptions.RemoveEmptyEntries);

            if (partes.Length != 2)
            {
                return null;
            }

            if (partes[0].Length == 2 || partes[0].Length == 4)
            {

            }
            else
            {
                return null;
            }

            if (partes[0].Length == 2)
            {
                partes[0] = "20" + partes[0];
            }

            if (partes[1].Length >= longitud)
            {
                partes[1] = partes[1].Substring((partes[1].Length - longitud));
            }
            else
            {
                partes[1] = partes[1].PadLeft(longitud, '0');
            }

            return partes[0] + '-' + partes[1];
        }

        private void EnviarButton_Click(object sender, EventArgs e)
        {
            ProcesarTabla();

        }

        private void InspectorButton_Click(object sender, EventArgs e)
        {
            if (!Application.OpenForms.OfType<InspectorForm>().Any())
            {
                InspectorForm inspector = new InspectorForm();
                inspector.Show();
            }
            else
            {
                Application.OpenForms.OfType<InspectorForm>().FirstOrDefault().Focus();
            }

        }

        private void ArchivoButton_Click(object sender, EventArgs e)
        {
            itemsGridView.CloseEditor();
            itemsGridView.UpdateCurrentRow();

            if (Globals.ThisAddIn.UserNameList.Count() == 0)
            {
                XtraMessageBox.Show("No existen cuentas en su configuración");
                return;
            }

            if (enProceso == true)
            {
                XtraMessageBox.Show("Los datos se estan procesando, por favor espere...");
                return;
            }

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                string pathFileName = dlg.FileName;
                
                DirectoryInfo directoryInfo = new DirectoryInfo(pathFileName);

                List<object> datos = new List<object>();

                datos.Add(2); //0 modo
                datos.Add(Path.GetFileNameWithoutExtension(directoryInfo.Name)); //1
                datos.Add(directoryInfo.Parent.FullName); //2
                datos.Add(Globals.ThisAddIn.UserNameList[0]); //3
                datos.Add(pathFileName); //4
                datos.Add(""); //5
                datos.Add(""); //6
                datos.Add(directoryInfo.CreationTime); //7

                ItemsGridControl_AgregarFilaPanel(datos);
            }
 
        }

        private void BorrarButton_Click(object sender, EventArgs e)
        {
            itemsGridView.CloseEditor();
            itemsGridView.UpdateCurrentRow();

            if (enProceso == true)
            {
                XtraMessageBox.Show("Los datos se estan procesando, por favor espere...");
                return;
            }

            if (itemsGridView.GetSelectedRows().Count() < 1)
            {
                XtraMessageBox.Show("No ha seleccionado ningún registro para eliminar.");
                return;

            }

            if (XtraMessageBox.Show("Esta seguro que desea eliminar los registros seleccionados?", "Confirme la acción", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                itemsGridView.DeleteSelectedRows();
            }
            
        }

    }
}
