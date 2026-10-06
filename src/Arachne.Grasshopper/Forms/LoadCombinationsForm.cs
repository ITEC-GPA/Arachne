using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Grasshopper;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;

namespace Arachne.Grasshopper.Forms
{
    public partial class LoadCombinationsForm : Form
    {
        protected LoadCaseModel[] _loadCases;
        protected List<GH_LoadCombination> _values;

        public List<GH_LoadCombination> Values => _values;

        /// <summary>
        /// Public contructor
        /// </summary>
        /// <param name="loadCases">The load cases names array to use for creating the columns of the DataGrid</param>
        /// <param name="values">The previous values to restore as rows of the DataGrid</param>
        public LoadCombinationsForm(LoadCaseModel[] loadCases, List<GH_LoadCombination> values = null)
        {
            Owner = Instances.DocumentEditor;
            InitializeComponent();
            _loadCases = loadCases;

            dataGridView.ColumnCount = loadCases.Length + 1;
            dataGridView.Columns[0].Name = "Combo Name";
            dataGridView.Columns[0].ValueType = typeof(string);
            for (int i = 0; i < loadCases.Length; i++)
            {
                dataGridView.Columns[i + 1].Name = loadCases[i].Name;
                dataGridView.Columns[i + 1].ValueType = typeof(double);
            }

            // Fill the current values
            if (values != null)
            {
                dataGridView.RowCount = values.Count + 1;
                for (int i = 0; i < values.Count; i++)
                {
                    dataGridView.Rows[i].Cells[0].Value = values[i].Value.Name;
                    foreach (KeyValuePair<LoadCaseModel, double> item in values[i].Value.Values)
                        dataGridView.Rows[i].Cells[item.Key.Name].Value = item.Value;
                }
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            _values = new List<GH_LoadCombination>();
            for (int i = 0; i < dataGridView.RowCount - 1; i++)
            {
                string name = dataGridView.Rows[i].Cells[0].Value.ToString();

                if (_loadCases.Count(lc => lc.Name == name) > 0)
                {
                    MessageBox.Show($"The combination names {name} is invalid. The name must be different to the load cases names",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    return;
                }


                if (_values.FirstOrDefault(c => c.Value.Name == name)!=null)
                {
                    MessageBox.Show($"The combination name {name} is used more than one time",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    return;
                }

                GH_LoadCombination combo = new GH_LoadCombination(name);
                for (int j = 0; j < dataGridView.ColumnCount - 1; j++)
                {
                    string lcase_name = dataGridView.Columns[j + 1].Name;
                    double incr = Convert.ToDouble(dataGridView.Rows[i].Cells[j + 1].Value);
                    LoadCaseModel lcase = _loadCases.Single(lc => lc.Name == lcase_name);
                    combo.Value.Values.Add(lcase, incr);
                }
                _values.Add(combo);
            }

            DialogResult = DialogResult.OK;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void dataGridView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                string s = Clipboard.GetText();
                string[] lines = s.Split('\n');
                int row = dataGridView.CurrentCell.RowIndex;
                int col = dataGridView.CurrentCell.ColumnIndex;
                if (row + lines.Length+1 > dataGridView.RowCount)
                {
                    dataGridView.RowCount = row + lines.Length+1;
                }
                foreach (string line in lines)
                {
                    if (row < dataGridView.RowCount && line.Length > 0)
                    {
                        string[] cells = line.Split('\t');
                        for (int i = 0; i < cells.GetLength(0); ++i)
                        {
                            if (col + i < this.dataGridView.ColumnCount)
                            {
                                try
                                {
                                    dataGridView[col + i, row].Value = Convert.ChangeType(cells[i], dataGridView[col + i, row].ValueType);
                                }
                                catch
                                {
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                        row++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }
}
