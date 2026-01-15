using System.Linq;
using System.Windows.Forms;

namespace TaskManagerPlus.Services
{
    public static class UILocalizer
    {
        public static void Apply(Control root)
        {
            ApplyControlRecursive(root);

            foreach (var ms in root.Controls.OfType<MenuStrip>())
                ApplyToolStripItems(ms.Items);

            foreach (var ts in root.Controls.OfType<ToolStrip>())
                ApplyToolStripItems(ts.Items);
        }

        private static void ApplyControlRecursive(Control parent)
        {
            ApplyOne(parent);

            foreach (Control child in parent.Controls)
                ApplyControlRecursive(child);
        }

        private static void ApplyOne(Control c)
        {
            if (c.Tag is string key && !string.IsNullOrWhiteSpace(key))
                c.Text = LocalizationService.T(key);

            if (c is DataGridView dgv)
            {
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    if (col.Tag is string colKey && !string.IsNullOrWhiteSpace(colKey))
                        col.HeaderText = LocalizationService.T(colKey);
                }
            }

            if (c is ListView lv)
            {
                foreach (ColumnHeader ch in lv.Columns)
                {
                    if (ch.Tag is string chKey && !string.IsNullOrWhiteSpace(chKey))
                        ch.Text = LocalizationService.T(chKey);
                }
            }

            if (c is ContextMenuStrip cms)
            {
                ApplyToolStripItems(cms.Items);
            }
        }

        private static void ApplyToolStripItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                if (item.Tag is string key && !string.IsNullOrWhiteSpace(key))
                    item.Text = LocalizationService.T(key);

                if (item is ToolStripMenuItem mi && mi.DropDownItems.Count > 0)
                    ApplyToolStripItems(mi.DropDownItems);
            }
        }
    }
}
