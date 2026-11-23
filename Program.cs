using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace EnvanterSayimApp
{
    // =========================================================================
    // DATA MODELS (SERIAL TABLE: NO | SERIAL NO | STATUS)
    // =========================================================================
    [Serializable]
    public class SerialRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SerialNo { get; set; } = "";
        public string Status { get; set; } = "";
    }

    [Serializable]
    public class ProductItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PhotoBase64 { get; set; }
    }

    [Serializable]
    public class RoomProductEntry
    {
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public List<SerialRecord> Serials { get; set; } = new List<SerialRecord>();
    }

    [Serializable]
    public class RoomData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public List<RoomProductEntry> Items { get; set; } = new List<RoomProductEntry>();

        public int GetQuantity(string productId)
        {
            var entry = Items.FirstOrDefault(x => x.ProductId == productId);
            return entry != null ? entry.Quantity : 0;
        }

        public List<SerialRecord> GetSerials(string productId)
        {
            var entry = Items.FirstOrDefault(x => x.ProductId == productId);
            if (entry == null) return new List<SerialRecord>();
            if (entry.Serials == null) entry.Serials = new List<SerialRecord>();
            return entry.Serials;
        }

        public bool HasProduct(string productId)
        {
            return Items.Any(x => x.ProductId == productId);
        }

        public void SetQuantity(string productId, int qty)
        {
            if (qty < 0) qty = 0;
            var entry = Items.FirstOrDefault(x => x.ProductId == productId);
            if (entry != null)
            {
                entry.Quantity = qty;
            }
            else
            {
                Items.Add(new RoomProductEntry { ProductId = productId, Quantity = qty });
            }
        }

        public void RemoveProduct(string productId)
        {
            Items.RemoveAll(x => x.ProductId == productId);
        }
    }

    [Serializable]
    public class InventoryStore
    {
        public string ViewMode { get; set; } = "list"; // "list" | "grid"
        public bool DarkMode { get; set; } = true;     // Dark / Light Mode
        public string Language { get; set; } = "tr";   // "tr" | "en"
        public int ColorTheme { get; set; } = 0;       // 0: Siyah & Beyaz, 1: Siyah & Altın, 2: Siyah & Kırmızı, 3: Gece Mavisi & Turkuaz
        public List<ProductItem> Products { get; set; } = new List<ProductItem>();
        public List<RoomData> Rooms { get; set; } = new List<RoomData>();
    }

    public class SmoothFlowPanel : FlowLayoutPanel
    {
        public SmoothFlowPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
    }

    // =========================================================================
    // HELPER: IMAGE, FULL TR/EN LOCALIZATION, 4 SLEEK THEMES & STORAGE
    // =========================================================================
    public static class AppHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void ApplyWindowTitleBarTheme(Form form, bool isDark)
        {
            try
            {
                int useDark = isDark ? 1 : 0;
                DwmSetWindowAttribute(form.Handle, 20, ref useDark, sizeof(int));
                DwmSetWindowAttribute(form.Handle, 19, ref useDark, sizeof(int));
            }
            catch { }
        }

        public static bool IsLaptopProduct(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName)) return false;
            return productName.IndexOf("laptop", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // İngilizce arayüzde kayıtlı varsayılan Türkçe açıklamaları ve terimleri de temiz İngilizce göster
        public static string LocalizeText(string raw, bool isEn)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            if (!isEn) return raw;

            string val = raw;
            if (val.Equals("Ana Envanter Odası", StringComparison.OrdinalIgnoreCase))
                return "Main Inventory Room";
            if (val.Equals("Aktif", StringComparison.OrdinalIgnoreCase))
                return "Active";
            if (val.Equals("Depoda", StringComparison.OrdinalIgnoreCase))
                return "In Stock";
            if (val.Equals("Arızalı", StringComparison.OrdinalIgnoreCase))
                return "Broken";
            if (val.Equals("Zimmetli", StringComparison.OrdinalIgnoreCase))
                return "Assigned";

            // Standart ürün adlarındaki Türkçe kelimeleri İngilizce modda akıllıca çevir
            val = ReplaceCaseInsensitive(val, "kablosuz klavye", "wireless keyboard");
            val = ReplaceCaseInsensitive(val, "kablolu klavye", "wired keyboard");
            val = ReplaceCaseInsensitive(val, "kablosuz mouse", "wireless mouse");
            val = ReplaceCaseInsensitive(val, "kablolu mouse", "wired mouse");
            val = ReplaceCaseInsensitive(val, "kulaklık", "headset");
            val = ReplaceCaseInsensitive(val, "klavye", "keyboard");
            return val;
        }

        private static string ReplaceCaseInsensitive(string input, string search, string replacement)
        {
            int idx = input.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return input;
            return input.Substring(0, idx) + replacement + input.Substring(idx + search.Length);
        }

        // =====================================================================
        // 4 ŞIK İKİLİ RENK PALETİ (SİYAH AĞIRLIKLI MODERN TEMALAR)
        // 0: Saf Siyah & Beyaz (Mat Karbon)
        // 1: Siyah & Altın
        // 2: Siyah & Kırmızı
        // 3: Gece Mavisi & Turkuaz
        // =====================================================================
        public static Color GetPrimaryColor(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(234, 179, 8) : Color.FromArgb(180, 83, 9);       // Siyah & Altın
                case 2: return dark ? Color.FromArgb(220, 38, 38) : Color.FromArgb(185, 28, 28);      // Siyah & Kırmızı
                case 3: return dark ? Color.FromArgb(14, 165, 233) : Color.FromArgb(2, 132, 199);     // Gece Mavisi & Turkuaz
                default: return dark ? Color.FromArgb(244, 244, 245) : Color.FromArgb(24, 24, 27);    // Saf Siyah & Beyaz
            }
        }

        public static Color GetPrimaryForeColor(int themeIdx, bool dark)
        {
            if (themeIdx == 0 && dark) return Color.FromArgb(9, 9, 11);      // Beyaz buton üstünde siyah yazı
            if (themeIdx == 1 && dark) return Color.FromArgb(15, 15, 12);    // Altın buton üstünde siyah yazı
            return Color.White;
        }

        public static Color GetSecondaryColor(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(245, 158, 11) : Color.FromArgb(41, 37, 36);      // Amber / Koyu Kömür
                case 2: return dark ? Color.FromArgb(248, 113, 113) : Color.FromArgb(30, 41, 59);     // Kırmızı Ton / Koyu Gri
                case 3: return dark ? Color.FromArgb(20, 184, 166) : Color.FromArgb(13, 148, 136);    // Turkuaz
                default: return dark ? Color.FromArgb(161, 161, 170) : Color.FromArgb(82, 82, 91);    // Gümüş / Koyu Gri
            }
        }

        public static Color GetSecondaryForeColor(int themeIdx, bool dark)
        {
            if (themeIdx == 0 && dark) return Color.FromArgb(9, 9, 11);
            if (themeIdx == 1 && dark) return Color.FromArgb(15, 15, 12);
            return Color.White;
        }

        public static Color GetPrimarySoftBg(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(52, 41, 16) : Color.FromArgb(254, 243, 199);
                case 2: return dark ? Color.FromArgb(66, 20, 24) : Color.FromArgb(254, 226, 226);
                case 3: return dark ? Color.FromArgb(15, 58, 92) : Color.FromArgb(224, 242, 254);
                default: return dark ? Color.FromArgb(39, 39, 44) : Color.FromArgb(228, 228, 231);
            }
        }

        public static Color GetSecondarySoftBg(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(64, 45, 14) : Color.FromArgb(254, 243, 199);
                case 2: return dark ? Color.FromArgb(66, 24, 28) : Color.FromArgb(254, 226, 226);
                case 3: return dark ? Color.FromArgb(19, 78, 74) : Color.FromArgb(204, 251, 241);
                default: return dark ? Color.FromArgb(34, 34, 40) : Color.FromArgb(244, 244, 245);
            }
        }

        public static Color GetBgColor(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(9, 9, 8) : Color.FromArgb(250, 250, 249);        // Mat Siyah / Kırık Beyaz
                case 2: return dark ? Color.FromArgb(10, 8, 9) : Color.FromArgb(248, 250, 252);       // Obsidyen Siyah / Açık Gri
                case 3: return dark ? Color.FromArgb(9, 14, 26) : Color.FromArgb(241, 245, 249);      // Gece Mavisi / Açık Buz
                default: return dark ? Color.FromArgb(8, 8, 10) : Color.FromArgb(244, 244, 245);      // Saf Siyah / Temiz Beyaz-Gri
            }
        }

        public static Color GetCardColor(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(20, 19, 16) : Color.White;                       // Koyu Karbon
                case 2: return dark ? Color.FromArgb(22, 16, 18) : Color.White;                       // Koyu Obsidyen
                case 3: return dark ? Color.FromArgb(17, 25, 44) : Color.White;                       // Koyu Lacivert
                default: return dark ? Color.FromArgb(18, 18, 21) : Color.White;                      // Mat Karbon Siyah
            }
        }

        public static Color GetSurfaceAltColor(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(36, 33, 27) : Color.FromArgb(245, 245, 244);
                case 2: return dark ? Color.FromArgb(38, 26, 30) : Color.FromArgb(241, 245, 249);
                case 3: return dark ? Color.FromArgb(28, 41, 68) : Color.FromArgb(226, 232, 240);
                default: return dark ? Color.FromArgb(30, 30, 36) : Color.FromArgb(228, 228, 231);
            }
        }

        public static Color GetInputBgColor(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(12, 12, 10) : Color.White;
                case 2: return dark ? Color.FromArgb(14, 10, 11) : Color.White;
                case 3: return dark ? Color.FromArgb(12, 18, 33) : Color.White;
                default: return dark ? Color.FromArgb(11, 11, 13) : Color.White;
            }
        }

        public static Color GetBorderColor(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(62, 54, 36) : Color.FromArgb(214, 211, 209);
                case 2: return dark ? Color.FromArgb(68, 38, 46) : Color.FromArgb(203, 213, 225);
                case 3: return dark ? Color.FromArgb(44, 64, 102) : Color.FromArgb(203, 213, 225);
                default: return dark ? Color.FromArgb(44, 44, 52) : Color.FromArgb(212, 212, 216);
            }
        }

        private static readonly Dictionary<string, Image> ImageCache = new Dictionary<string, Image>();

        public static string DataFilePath
        {
            get
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(dir, "envanter_verisi.xml");
            }
        }

        public static void ClearImageCache(string productId = null)
        {
            if (productId == null)
            {
                foreach (var img in ImageCache.Values) img.Dispose();
                ImageCache.Clear();
            }
            else if (ImageCache.ContainsKey(productId))
            {
                ImageCache[productId].Dispose();
                ImageCache.Remove(productId);
            }
        }

        public static Image GetProductImage(ProductItem product)
        {
            if (product == null || string.IsNullOrEmpty(product.PhotoBase64))
                return null;

            if (ImageCache.TryGetValue(product.Id, out Image cached))
                return cached;

            try
            {
                byte[] bytes = Convert.FromBase64String(product.PhotoBase64);
                using (var ms = new MemoryStream(bytes))
                {
                    Image loaded = Image.FromStream(ms);
                    Bitmap clone = new Bitmap(loaded);
                    loaded.Dispose();
                    ImageCache[product.Id] = clone;
                    return clone;
                }
            }
            catch
            {
                return null;
            }
        }

        public static string LoadAndResizeImageToBase64(string filePath, int maxSize = 400)
        {
            using (Image src = Image.FromFile(filePath))
            {
                int w = src.Width;
                int h = src.Height;
                if (w > h && w > maxSize)
                {
                    h = (int)Math.Round((double)h * maxSize / w);
                    w = maxSize;
                }
                else if (h >= w && h > maxSize)
                {
                    w = (int)Math.Round((double)w * maxSize / h);
                    h = maxSize;
                }

                using (Bitmap bmp = new Bitmap(w, h))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Jpeg);
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }

        public static void SaveStore(InventoryStore store)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(InventoryStore));
                using (StreamWriter sw = new StreamWriter(DataFilePath, false, Encoding.UTF8))
                {
                    serializer.Serialize(sw, store);
                }
            }
            catch (Exception ex)
            {
                string title = store.Language == "en" ? "Save Error" : "Kayıt Hatası";
                MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static InventoryStore LoadStore()
        {
            try
            {
                if (File.Exists(DataFilePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(InventoryStore));
                    using (StreamReader sr = new StreamReader(DataFilePath, Encoding.UTF8))
                    {
                        var loaded = (InventoryStore)serializer.Deserialize(sr);
                        if (loaded != null && (loaded.Rooms.Count > 0 || loaded.Products.Count > 0))
                        {
                            if (string.IsNullOrEmpty(loaded.Language)) loaded.Language = "tr";
                            return loaded;
                        }
                    }
                }
            }
            catch { }

            var initial = SeedData.CreateInitialStore(true);
            SaveStore(initial);
            return initial;
        }
    }

    // =========================================================================
    // MAIN DESKTOP WINDOW
    // =========================================================================
    public class MainForm : Form
    {
        private InventoryStore store;
        private string activeTab = "rooms";
        private string currentRoomId = null;

        private bool IsEN => store.Language == "en";
        private string T(string tr, string en) => IsEN ? en : tr;
        private string L(string raw) => AppHelper.LocalizeText(raw, IsEN);

        // Dynamic Theme Palette
        private Color ColPrimary => AppHelper.GetPrimaryColor(store.ColorTheme, store.DarkMode);
        private Color ColPrimaryFore => AppHelper.GetPrimaryForeColor(store.ColorTheme, store.DarkMode);
        private Color ColSecondary => AppHelper.GetSecondaryColor(store.ColorTheme, store.DarkMode);
        private Color ColSecondaryFore => AppHelper.GetSecondaryForeColor(store.ColorTheme, store.DarkMode);
        private Color ColPrimarySoft => AppHelper.GetPrimarySoftBg(store.ColorTheme, store.DarkMode);

        private Color ColBg => AppHelper.GetBgColor(store.ColorTheme, store.DarkMode);
        private Color ColCard => AppHelper.GetCardColor(store.ColorTheme, store.DarkMode);
        private Color ColSurfaceAlt => AppHelper.GetSurfaceAltColor(store.ColorTheme, store.DarkMode);
        private Color ColInputBg => AppHelper.GetInputBgColor(store.ColorTheme, store.DarkMode);
        private Color ColTextDark => store.DarkMode ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
        private Color ColTextMuted => store.DarkMode ? Color.FromArgb(161, 161, 170) : Color.FromArgb(82, 82, 91);
        private Color ColBorder => AppHelper.GetBorderColor(store.ColorTheme, store.DarkMode);
        private Color ColDanger => store.DarkMode ? Color.FromArgb(251, 113, 133) : Color.FromArgb(220, 38, 38);
        private Color ColDangerBg => store.DarkMode ? Color.FromArgb(64, 14, 22) : Color.FromArgb(254, 226, 226);

        // UI Controls
        private Panel topBar;
        private Label lblTopLeftSettings; // KUTUCUKSIZ SADECE YAZI AYARLAR TUŞU
        private Label lblLogo;
        private Button btnTabRooms;
        private Button btnTabCatalog;
        private Button btnTabSummary;
        private Button btnLaptopExcel;
        private Button btnImportExcel;
        private Button btnExportCsv;

        private Panel sidebarPanel;
        private Panel sidebarTop;
        private Label lblRoomsHeader;
        private Button btnAddRoom;
        private Label lblRoomSearchHint;
        private TextBox txtRoomSearch;
        private SmoothFlowPanel roomsListPanel;

        private Panel mainArea;
        private Panel contentHeaderPanel;
        private Label lblContentTitle;
        private Label lblContentStats;
        private Label lblProdSearch;
        private Button btnEditRoom;
        private Button btnDeleteRoom;
        private Button btnPrimaryAction;
        private TextBox txtProductSearch;
        private Button btnViewList;
        private Button btnViewGrid;

        private SmoothFlowPanel mainCardsPanel;

        public MainForm()
        {
            this.Size = new Size(1280, 800);
            this.MinimumSize = new Size(1060, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.DoubleBuffered = true;

            store = AppHelper.LoadStore();
            if (store.Rooms.Count > 0)
                currentRoomId = store.Rooms[0].Id;

            BuildUI();
            ApplyThemeAndLanguage();
            RefreshAll();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AppHelper.ApplyWindowTitleBarTheme(this, store != null && store.DarkMode);
        }

        private void BuildUI()
        {
            // 1. TOP HEADER BAR
            topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                Padding = new Padding(14, 10, 16, 10)
            };
            topBar.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawLine(p, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
            };

            // SOL ÜST: KUTUCUKSUZ SADECE YAZI "Ayarlar" / "Settings"
            lblTopLeftSettings = new Label
            {
                Text = "Ayarlar",
                Location = new Point(18, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            lblTopLeftSettings.Click += (s, e) => OpenTopLeftSettingsDialog();
            lblTopLeftSettings.MouseEnter += (s, e) => lblTopLeftSettings.ForeColor = ColSecondary;
            lblTopLeftSettings.MouseLeave += (s, e) => lblTopLeftSettings.ForeColor = ColTextDark;
            topBar.Controls.Add(lblTopLeftSettings);

            // Divider line between Settings text and Logo
            Label lblLogoDivider = new Label
            {
                Text = "|",
                Location = new Point(102, 19),
                AutoSize = true,
                Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                ForeColor = Color.Gray
            };
            topBar.Controls.Add(lblLogoDivider);

            // Title
            lblLogo = new Label
            {
                Text = "Envanter Sayım",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(122, 18)
            };
            topBar.Controls.Add(lblLogo);

            // Center Tabs
            btnTabRooms = CreateTabButton("", 270, 134);
            btnTabRooms.Click += (s, e) => SwitchTab("rooms");

            btnTabCatalog = CreateTabButton("", 410, 134);
            btnTabCatalog.Click += (s, e) => SwitchTab("catalog");

            btnTabSummary = CreateTabButton("", 550, 134);
            btnTabSummary.Click += (s, e) => SwitchTab("summary");

            topBar.Controls.Add(btnTabRooms);
            topBar.Controls.Add(btnTabCatalog);
            topBar.Controls.Add(btnTabSummary);

            // Right Actions: Laptop Excel + General Excel Import & Export
            FlowLayoutPanel rightActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };

            btnExportCsv = new Button
            {
                Text = "",
                Size = new Size(110, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            btnExportCsv.FlatAppearance.BorderSize = 0;
            btnExportCsv.Click += (s, e) => ExportToExcel();

            btnImportExcel = new Button
            {
                Text = "",
                Size = new Size(110, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            btnImportExcel.FlatAppearance.BorderSize = 1;
            btnImportExcel.Click += (s, e) => ImportFromExcel();

            btnLaptopExcel = new Button
            {
                Text = "",
                Size = new Size(124, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            btnLaptopExcel.FlatAppearance.BorderSize = 0;
            btnLaptopExcel.Click += (s, e) => OpenLaptopExcelDialog();

            rightActions.Controls.Add(btnExportCsv);
            rightActions.Controls.Add(btnImportExcel);
            rightActions.Controls.Add(btnLaptopExcel);
            topBar.Controls.Add(rightActions);

            // 2. LEFT SIDEBAR (ROOMS)
            sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 290,
                Padding = new Padding(12)
            };
            sidebarPanel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawLine(p, sidebarPanel.Width - 1, 0, sidebarPanel.Width - 1, sidebarPanel.Height);
            };

            sidebarTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 115
            };

            lblRoomsHeader = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(4, 6),
                AutoSize = true
            };

            btnAddRoom = new Button
            {
                Text = "",
                Location = new Point(4, 28),
                Size = new Size(256, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAddRoom.FlatAppearance.BorderSize = 0;
            btnAddRoom.Click += (s, e) => OpenRoomDialog(null);

            lblRoomSearchHint = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                Location = new Point(4, 74),
                AutoSize = true
            };

            txtRoomSearch = new TextBox
            {
                Location = new Point(66, 71),
                Size = new Size(194, 26),
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtRoomSearch.TextChanged += (s, e) => RenderRoomsSidebar();

            sidebarTop.Controls.Add(lblRoomsHeader);
            sidebarTop.Controls.Add(btnAddRoom);
            sidebarTop.Controls.Add(lblRoomSearchHint);
            sidebarTop.Controls.Add(txtRoomSearch);

            roomsListPanel = new SmoothFlowPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 12)
            };

            sidebarPanel.Controls.Add(roomsListPanel);
            sidebarPanel.Controls.Add(sidebarTop);

            // 3. MAIN CONTENT AREA
            mainArea = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16)
            };

            contentHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 112
            };
            contentHeaderPanel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, contentHeaderPanel.Width - 1, contentHeaderPanel.Height - 1);
            };

            lblContentTitle = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            };

            lblContentStats = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(18, 42),
                AutoSize = true
            };

            btnEditRoom = new Button
            {
                Text = "",
                Size = new Size(115, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnEditRoom.FlatAppearance.BorderSize = 0;
            btnEditRoom.Click += (s, e) =>
            {
                var room = store.Rooms.FirstOrDefault(r => r.Id == currentRoomId);
                if (room != null) OpenRoomDialog(room);
            };

            btnDeleteRoom = new Button
            {
                Text = "",
                Size = new Size(96, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDeleteRoom.FlatAppearance.BorderSize = 0;
            btnDeleteRoom.Click += (s, e) => DeleteCurrentRoom();

            btnPrimaryAction = new Button
            {
                Text = "",
                Size = new Size(195, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPrimaryAction.FlatAppearance.BorderSize = 0;
            btnPrimaryAction.Click += (s, e) => OnPrimaryActionClicked();

            lblProdSearch = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(18, 76),
                AutoSize = true
            };

            txtProductSearch = new TextBox
            {
                Location = new Point(172, 73),
                Size = new Size(255, 28),
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtProductSearch.TextChanged += (s, e) => RenderMainCards();

            btnViewList = new Button
            {
                Text = "",
                Size = new Size(135, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnViewList.FlatAppearance.BorderSize = 1;
            btnViewList.Click += (s, e) => SetViewMode("list");

            btnViewGrid = new Button
            {
                Text = "",
                Size = new Size(135, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnViewGrid.FlatAppearance.BorderSize = 1;
            btnViewGrid.Click += (s, e) => SetViewMode("grid");

            contentHeaderPanel.Controls.Add(lblContentTitle);
            contentHeaderPanel.Controls.Add(lblContentStats);
            contentHeaderPanel.Controls.Add(btnEditRoom);
            contentHeaderPanel.Controls.Add(btnDeleteRoom);
            contentHeaderPanel.Controls.Add(btnPrimaryAction);
            contentHeaderPanel.Controls.Add(lblProdSearch);
            contentHeaderPanel.Controls.Add(txtProductSearch);
            contentHeaderPanel.Controls.Add(btnViewList);
            contentHeaderPanel.Controls.Add(btnViewGrid);

            contentHeaderPanel.Resize += (s, e) => LayoutContentHeader();

            mainCardsPanel = new SmoothFlowPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(0, 12, 0, 20)
            };
            mainCardsPanel.Resize += (s, e) => AdjustListCardWidths();

            mainArea.Controls.Add(mainCardsPanel);
            mainArea.Controls.Add(contentHeaderPanel);

            this.Controls.Add(mainArea);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(topBar);

            LayoutContentHeader();
        }

        // =====================================================================
        // TOP-LEFT SETTINGS DIALOG (LANGUAGE + DARK/LIGHT + 4 SLEEK DUAL-COLOR THEMES)
        // =====================================================================
        private void OpenTopLeftSettingsDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = T("Ayarlar", "Settings");
                dlg.Size = new Size(460, 505);
                dlg.StartPosition = FormStartPosition.Manual;
                Point screenPt = this.PointToScreen(new Point(14, 58));
                dlg.Location = screenPt;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = ColCard;
                dlg.ForeColor = ColTextDark;
                dlg.Font = new Font("Segoe UI", 9.5f);
                dlg.HandleCreated += (s, e) => AppHelper.ApplyWindowTitleBarTheme(dlg, store.DarkMode);

                // 1. Language
                Label lblLang = new Label
                {
                    Text = T("Arayüz Dili", "Interface Language"),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(20, 16),
                    AutoSize = true
                };

                Button btnTR = new Button
                {
                    Text = "Türkçe (TR)",
                    Location = new Point(20, 42),
                    Size = new Size(195, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = !IsEN ? ColPrimary : ColSurfaceAlt,
                    ForeColor = !IsEN ? ColPrimaryFore : ColTextDark,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnTR.FlatAppearance.BorderSize = 0;

                Button btnEN = new Button
                {
                    Text = "English (EN)",
                    Location = new Point(225, 42),
                    Size = new Size(195, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = IsEN ? ColPrimary : ColSurfaceAlt,
                    ForeColor = IsEN ? ColPrimaryFore : ColTextDark,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnEN.FlatAppearance.BorderSize = 0;

                btnTR.Click += (s, e) =>
                {
                    store.Language = "tr";
                    AppHelper.SaveStore(store);
                    ApplyThemeAndLanguage();
                    RefreshAll();
                    dlg.Close();
                };

                btnEN.Click += (s, e) =>
                {
                    store.Language = "en";
                    AppHelper.SaveStore(store);
                    ApplyThemeAndLanguage();
                    RefreshAll();
                    dlg.Close();
                };

                // 2. Dark / Light Mode
                Label lblMode = new Label
                {
                    Text = T("Görünüm Modu", "Appearance Mode"),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(20, 96),
                    AutoSize = true
                };

                Button btnDark = new Button
                {
                    Text = T("Koyu Mod", "Dark Mode"),
                    Location = new Point(20, 122),
                    Size = new Size(195, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = store.DarkMode ? ColPrimary : ColSurfaceAlt,
                    ForeColor = store.DarkMode ? ColPrimaryFore : ColTextDark,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnDark.FlatAppearance.BorderSize = 0;

                Button btnLight = new Button
                {
                    Text = T("Açık Mod", "Light Mode"),
                    Location = new Point(225, 122),
                    Size = new Size(195, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = !store.DarkMode ? ColPrimary : ColSurfaceAlt,
                    ForeColor = !store.DarkMode ? ColPrimaryFore : ColTextDark,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnLight.FlatAppearance.BorderSize = 0;

                btnDark.Click += (s, e) =>
                {
                    store.DarkMode = true;
                    AppHelper.SaveStore(store);
                    AppHelper.ApplyWindowTitleBarTheme(this, true);
                    ApplyThemeAndLanguage();
                    RefreshAll();
                    dlg.Close();
                };

                btnLight.Click += (s, e) =>
                {
                    store.DarkMode = false;
                    AppHelper.SaveStore(store);
                    AppHelper.ApplyWindowTitleBarTheme(this, false);
                    ApplyThemeAndLanguage();
                    RefreshAll();
                    dlg.Close();
                };

                // 3. 4 Sleek Dual-Color Options (Starting with Pure Black)
                Label lblColors = new Label
                {
                    Text = T("4 İkili Renk Seçeneği", "4 Dual-Color Theme Options"),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(20, 178),
                    AutoSize = true
                };

                string[] themeNames = new[]
                {
                    T("1) Saf Siyah & Beyaz (Mat Karbon)", "1) Pure Black & White (Matte Carbon)"),
                    T("2) Siyah & Altın", "2) Matte Black & Gold"),
                    T("3) Siyah & Kırmızı", "3) Obsidian Black & Red"),
                    T("4) Gece Mavisi & Turkuaz", "4) Midnight Blue & Cyan")
                };

                dlg.Controls.AddRange(new Control[] { lblLang, btnTR, btnEN, lblMode, btnDark, btnLight, lblColors });

                for (int i = 0; i < 4; i++)
                {
                    int idx = i;
                    bool isCurrent = store.ColorTheme == idx;
                    Color pCol = AppHelper.GetPrimaryColor(idx, store.DarkMode);
                    Color pFore = AppHelper.GetPrimaryForeColor(idx, store.DarkMode);
                    Color sCol = AppHelper.GetSecondaryColor(idx, store.DarkMode);
                    Color bgCol = AppHelper.GetBgColor(idx, store.DarkMode);

                    Button btnTheme = new Button
                    {
                        Text = (isCurrent ? "✓  " : "    ") + themeNames[idx],
                        Location = new Point(20, 206 + (idx * 44)),
                        Size = new Size(400, 38),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(12, 0, 0, 0),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = isCurrent ? pCol : bgCol,
                        ForeColor = isCurrent ? pFore : ColTextDark,
                        Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
                        Cursor = Cursors.Hand
                    };
                    btnTheme.FlatAppearance.BorderColor = isCurrent ? sCol : ColBorder;
                    btnTheme.FlatAppearance.BorderSize = isCurrent ? 2 : 1;

                    btnTheme.Paint += (sender, pe) =>
                    {
                        using (SolidBrush b1 = new SolidBrush(bgCol))
                        using (SolidBrush b2 = new SolidBrush(pCol))
                        using (Pen borderPen = new Pen(Color.Gray, 1f))
                        {
                            pe.Graphics.FillRectangle(b1, btnTheme.Width - 62, 8, 22, 22);
                            pe.Graphics.DrawRectangle(borderPen, btnTheme.Width - 62, 8, 22, 22);
                            pe.Graphics.FillRectangle(b2, btnTheme.Width - 34, 8, 22, 22);
                            pe.Graphics.DrawRectangle(borderPen, btnTheme.Width - 34, 8, 22, 22);
                        }
                    };

                    btnTheme.Click += (s, e) =>
                    {
                        store.ColorTheme = idx;
                        AppHelper.SaveStore(store);
                        ApplyThemeAndLanguage();
                        RefreshAll();
                        dlg.Close();
                    };

                    dlg.Controls.Add(btnTheme);
                }

                // 4. Bottom Data Controls
                Button btnRestoreInventory = new Button
                {
                    Text = T("Inventory Odasını Geri Yükle", "Restore Default Inventory"),
                    Location = new Point(20, 398),
                    Size = new Size(215, 36),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColSurfaceAlt,
                    ForeColor = ColTextDark,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnRestoreInventory.FlatAppearance.BorderSize = 0;
                btnRestoreInventory.Click += (s, e) =>
                {
                    bool dark = store.DarkMode;
                    string lang = store.Language;
                    int theme = store.ColorTheme;
                    AppHelper.ClearImageCache();
                    store = SeedData.CreateInitialStore(dark);
                    store.Language = lang;
                    store.ColorTheme = theme;
                    currentRoomId = store.Rooms.FirstOrDefault()?.Id;
                    AppHelper.SaveStore(store);
                    RefreshAll();
                    dlg.Close();
                };

                Button btnResetAll = new Button
                {
                    Text = T("Tüm Verileri Sıfırla", "Reset All Data"),
                    Location = new Point(245, 398),
                    Size = new Size(175, 36),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColDangerBg,
                    ForeColor = ColDanger,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnResetAll.FlatAppearance.BorderSize = 0;
                btnResetAll.Click += (s, e) =>
                {
                    dlg.Close();
                    ResetAllData();
                };

                dlg.Controls.Add(btnRestoreInventory);
                dlg.Controls.Add(btnResetAll);
                dlg.ShowDialog(this);
            }
        }

        private void ApplyThemeAndLanguage()
        {
            this.Text = T("Envanter & Oda Sayım Uygulaması", "Inventory & Room Counting Application");
            this.BackColor = ColBg;

            topBar.BackColor = ColCard;

            // KUTUCUKSUZ SADECE YAZI AYARLAR TUŞU
            lblTopLeftSettings.Text = T("Ayarlar", "Settings");
            lblTopLeftSettings.BackColor = ColCard;
            lblTopLeftSettings.ForeColor = ColTextDark;

            lblLogo.Text = T("Envanter Sayım", "Inventory Count");
            lblLogo.ForeColor = ColTextDark;

            btnTabRooms.Text = T("Odalar ve Sayım", "Rooms & Count");
            btnTabCatalog.Text = T("Ürün Havuzu", "Product Pool");
            btnTabSummary.Text = T("Genel Özet", "Global Summary");

            btnLaptopExcel.Text = T("Laptop Excel", "Laptop Excel");
            btnLaptopExcel.BackColor = ColPrimary;
            btnLaptopExcel.ForeColor = ColPrimaryFore;

            btnExportCsv.Text = T("Excel İndir", "Export Excel");
            btnExportCsv.BackColor = ColSecondary;
            btnExportCsv.ForeColor = ColSecondaryFore;

            btnImportExcel.Text = T("Excel Yükle", "Import Excel");
            btnImportExcel.BackColor = ColSurfaceAlt;
            btnImportExcel.ForeColor = ColTextDark;
            btnImportExcel.FlatAppearance.BorderColor = ColBorder;

            sidebarPanel.BackColor = ColCard;
            sidebarTop.BackColor = ColCard;
            lblRoomsHeader.Text = T("ODALAR / BÖLGELER", "ROOMS / LOCATIONS");
            lblRoomsHeader.ForeColor = ColTextMuted;

            btnAddRoom.Text = T("+ Yeni Oda Ekle", "+ Add New Room");
            btnAddRoom.BackColor = ColPrimary;
            btnAddRoom.ForeColor = ColPrimaryFore;

            lblRoomSearchHint.Text = T("Ara:", "Find:");
            lblRoomSearchHint.ForeColor = ColTextMuted;
            txtRoomSearch.BackColor = ColInputBg;
            txtRoomSearch.ForeColor = ColTextDark;
            roomsListPanel.BackColor = ColCard;

            mainArea.BackColor = ColBg;
            contentHeaderPanel.BackColor = ColCard;
            lblContentTitle.ForeColor = ColTextDark;
            lblContentStats.ForeColor = ColSecondary;

            btnEditRoom.Text = T("Odayı Düzenle", "Edit Room");
            btnEditRoom.BackColor = ColSurfaceAlt;
            btnEditRoom.ForeColor = ColTextDark;

            btnDeleteRoom.Text = T("Odayı Sil", "Delete Room");
            btnDeleteRoom.BackColor = ColDangerBg;
            btnDeleteRoom.ForeColor = ColDanger;

            btnPrimaryAction.BackColor = ColPrimary;
            btnPrimaryAction.ForeColor = ColPrimaryFore;

            lblProdSearch.Text = T("Ürün / Seri No Ara:", "Search Item / SN:");
            lblProdSearch.ForeColor = ColTextMuted;
            txtProductSearch.BackColor = ColInputBg;
            txtProductSearch.ForeColor = ColTextDark;

            btnViewList.Text = T("☰ Dümdüz Liste", "☰ Flat List");
            btnViewGrid.Text = T("⊞ Kare Kare", "⊞ Square Grid");

            mainCardsPanel.BackColor = ColBg;

            topBar.Invalidate();
            sidebarPanel.Invalidate();
            contentHeaderPanel.Invalidate();
        }

        private void LayoutContentHeader()
        {
            int w = contentHeaderPanel.Width;
            btnPrimaryAction.Location = new Point(w - btnPrimaryAction.Width - 16, 14);
            btnDeleteRoom.Location = new Point(btnPrimaryAction.Left - btnDeleteRoom.Width - 10, 19);
            btnEditRoom.Location = new Point(btnDeleteRoom.Left - btnEditRoom.Width - 8, 19);

            btnViewGrid.Location = new Point(w - btnViewGrid.Width - 16, 68);
            btnViewList.Location = new Point(btnViewGrid.Left - btnViewList.Width - 8, 68);
        }

        private Button CreateTabButton(string text, int x, int width)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, 12),
                Size = new Size(width, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void SwitchTab(string tab)
        {
            activeTab = tab;
            txtProductSearch.Text = "";
            RefreshAll();
        }

        private void SetViewMode(string mode)
        {
            store.ViewMode = mode;
            AppHelper.SaveStore(store);
            UpdateViewToggleStyle();
            RenderMainCards();
        }

        private void UpdateViewToggleStyle()
        {
            bool isList = store.ViewMode == "list";
            btnViewList.BackColor = isList ? ColPrimary : ColSurfaceAlt;
            btnViewList.ForeColor = isList ? ColPrimaryFore : ColTextDark;
            btnViewList.FlatAppearance.BorderColor = isList ? ColPrimary : ColBorder;

            btnViewGrid.BackColor = !isList ? ColPrimary : ColSurfaceAlt;
            btnViewGrid.ForeColor = !isList ? ColPrimaryFore : ColTextDark;
            btnViewGrid.FlatAppearance.BorderColor = !isList ? ColPrimary : ColBorder;
        }

        private void UpdateTabButtonStyles()
        {
            var tabs = new Dictionary<string, Button>
            {
                { "rooms", btnTabRooms },
                { "catalog", btnTabCatalog },
                { "summary", btnTabSummary }
            };

            foreach (var kv in tabs)
            {
                bool active = kv.Key == activeTab;
                kv.Value.BackColor = active ? ColPrimary : ColSurfaceAlt;
                kv.Value.ForeColor = active ? ColPrimaryFore : ColTextMuted;
            }
        }

        public void RefreshAll()
        {
            UpdateTabButtonStyles();
            UpdateViewToggleStyle();

            sidebarPanel.Visible = (activeTab == "rooms");

            if (activeTab == "rooms")
            {
                if (currentRoomId == null && store.Rooms.Count > 0)
                    currentRoomId = store.Rooms[0].Id;

                RenderRoomsSidebar();
            }

            RenderMainCards();
        }

        // =====================================================================
        // LEFT SIDEBAR: ROOMS LIST
        // =====================================================================
        private void RenderRoomsSidebar()
        {
            roomsListPanel.SuspendLayout();
            foreach (Control c in roomsListPanel.Controls) c.Dispose();
            roomsListPanel.Controls.Clear();

            string q = (txtRoomSearch.Text ?? "").Trim().ToLowerInvariant();
            var filtered = store.Rooms.Where(r =>
                r.Name.ToLowerInvariant().Contains(q) ||
                (!string.IsNullOrEmpty(r.Note) && L(r.Note).ToLowerInvariant().Contains(q))
            ).ToList();

            foreach (var room in filtered)
            {
                bool isSelected = (room.Id == currentRoomId);
                int validVariety = room.Items.Count(i => store.Products.Any(p => p.Id == i.ProductId));
                int totalQty = room.Items.Where(i => store.Products.Any(p => p.Id == i.ProductId)).Sum(i => i.Quantity);

                Panel card = new Panel
                {
                    Size = new Size(252, 72),
                    BackColor = isSelected ? ColPrimarySoft : ColBg,
                    Margin = new Padding(4, 3, 4, 3),
                    Cursor = Cursors.Hand
                };
                card.Paint += (s, e) =>
                {
                    Color borderCol = isSelected ? ColPrimary : ColBorder;
                    int bw = isSelected ? 2 : 1;
                    using (Pen p = new Pen(borderCol, bw))
                        e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                };

                Label lblName = new Label
                {
                    Text = room.Name,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(10, 8),
                    Size = new Size(230, 22),
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };

                Label lblNote = new Label
                {
                    Text = string.IsNullOrEmpty(room.Note) ? T("Oda Envanteri", "Room Inventory") : L(room.Note),
                    Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                    ForeColor = ColTextMuted,
                    Location = new Point(10, 29),
                    Size = new Size(230, 16),
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };

                Label lblCounts = new Label
                {
                    Text = IsEN
                        ? $"{validVariety} Items  •  Total {totalQty}"
                        : $"{validVariety} Çeşit Ürün  •  Toplam {totalQty} Adet",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = ColSecondary,
                    Location = new Point(10, 47),
                    AutoSize = true,
                    Cursor = Cursors.Hand
                };

                EventHandler selectRoomHandler = (s, e) =>
                {
                    currentRoomId = room.Id;
                    RenderRoomsSidebar();
                    RenderMainCards();
                };

                card.Click += selectRoomHandler;
                lblName.Click += selectRoomHandler;
                lblNote.Click += selectRoomHandler;
                lblCounts.Click += selectRoomHandler;

                card.Controls.Add(lblName);
                card.Controls.Add(lblNote);
                card.Controls.Add(lblCounts);

                roomsListPanel.Controls.Add(card);
            }

            roomsListPanel.ResumeLayout();
        }

        // =====================================================================
        // MAIN CARDS AREA
        // =====================================================================
        private void RenderMainCards()
        {
            mainCardsPanel.SuspendLayout();
            foreach (Control c in mainCardsPanel.Controls) c.Dispose();
            mainCardsPanel.Controls.Clear();

            bool isList = (store.ViewMode == "list");
            mainCardsPanel.FlowDirection = isList ? FlowDirection.TopDown : FlowDirection.LeftToRight;
            mainCardsPanel.WrapContents = !isList;

            string query = (txtProductSearch.Text ?? "").Trim().ToLowerInvariant();

            if (activeTab == "rooms")
            {
                var room = store.Rooms.FirstOrDefault(r => r.Id == currentRoomId);
                if (room == null)
                {
                    lblContentTitle.Text = T("Henüz Oda Seçilmedi", "No Room Selected");
                    lblContentStats.Text = T("Sol menüden yeni bir oda ekleyin veya seçin.", "Add or select a room from the left sidebar.");
                    btnEditRoom.Visible = false;
                    btnDeleteRoom.Visible = false;
                    btnPrimaryAction.Visible = false;
                    mainCardsPanel.ResumeLayout();
                    return;
                }

                btnEditRoom.Visible = true;
                btnDeleteRoom.Visible = true;
                btnPrimaryAction.Visible = true;
                btnPrimaryAction.Text = T("+ Odaya Ürün Ekle", "+ Add Item to Room");

                UpdateCurrentRoomHeaderStats(room);

                var roomProducts = room.Items
                    .Select(entry => new
                    {
                        Product = store.Products.FirstOrDefault(p => p.Id == entry.ProductId),
                        Entry = entry
                    })
                    .Where(x => x.Product != null && (
                        x.Product.Name.ToLowerInvariant().Contains(query) ||
                        L(x.Product.Name).ToLowerInvariant().Contains(query) ||
                        (x.Entry.Serials != null && x.Entry.Serials.Any(sr =>
                            (sr.SerialNo ?? "").ToLowerInvariant().Contains(query) ||
                            L(sr.Status ?? "").ToLowerInvariant().Contains(query)))
                    ))
                    .ToList();

                if (roomProducts.Count == 0)
                {
                    mainCardsPanel.Controls.Add(CreateEmptyStatePanel(
                        T("Bu Odada Ürün Bulunamadı", "No Items Found in This Room"),
                        T("Sağ üstteki '+ Odaya Ürün Ekle' butonuna tıklayarak ürün ekleyebilirsiniz.", "Click '+ Add Item to Room' at the top right to add products.")
                    ));
                }
                else
                {
                    int listWidth = Math.Max(520, mainCardsPanel.ClientSize.Width - 28);
                    foreach (var item in roomProducts)
                    {
                        Control card = isList
                            ? CreateRoomProductListCard(room, item.Product, item.Entry.Quantity, listWidth)
                            : CreateRoomProductGridCard(room, item.Product, item.Entry.Quantity);
                        mainCardsPanel.Controls.Add(card);
                    }
                }
            }
            else if (activeTab == "catalog")
            {
                btnEditRoom.Visible = false;
                btnDeleteRoom.Visible = false;
                btnPrimaryAction.Visible = true;
                btnPrimaryAction.Text = T("+ Havuza Ürün Ekle", "+ New Catalog Item");

                lblContentTitle.Text = T("Genel Ürün Havuzu (Katalog)", "Global Product Pool (Catalog)");
                lblContentStats.Text = IsEN
                    ? $"Total {store.Products.Count} Registered Products"
                    : $"Toplam {store.Products.Count} Kayıtlı Ürün Çeşidi";

                var products = store.Products
                    .Where(p => p.Name.ToLowerInvariant().Contains(query) || L(p.Name).ToLowerInvariant().Contains(query))
                    .ToList();

                int listWidth = Math.Max(520, mainCardsPanel.ClientSize.Width - 28);
                foreach (var prod in products)
                {
                    Control card = isList
                        ? CreateCatalogSummaryCardList(prod, listWidth, true)
                        : CreateCatalogSummaryCardGrid(prod, true);
                    mainCardsPanel.Controls.Add(card);
                }
            }
            else if (activeTab == "summary")
            {
                btnEditRoom.Visible = false;
                btnDeleteRoom.Visible = false;
                btnPrimaryAction.Visible = false;

                int totalSayim = store.Rooms.Sum(r => r.Items.Where(i => store.Products.Any(p => p.Id == i.ProductId)).Sum(i => i.Quantity));
                lblContentTitle.Text = T("Genel Özet & Oda Dağılım Raporu", "Global Summary & Room Breakdown");
                lblContentStats.Text = IsEN
                    ? $"{store.Rooms.Count} Rooms  •  {store.Products.Count} Products  •  Grand Total: {totalSayim}"
                    : $"{store.Rooms.Count} Oda  •  {store.Products.Count} Ürün Çeşidi  •  Genel Toplam: {totalSayim} Adet";

                var products = store.Products
                    .Where(p => p.Name.ToLowerInvariant().Contains(query) || L(p.Name).ToLowerInvariant().Contains(query))
                    .ToList();

                int listWidth = Math.Max(520, mainCardsPanel.ClientSize.Width - 28);
                foreach (var prod in products)
                {
                    Control card = isList
                        ? CreateCatalogSummaryCardList(prod, listWidth, false)
                        : CreateCatalogSummaryCardGrid(prod, false);
                    mainCardsPanel.Controls.Add(card);
                }
            }

            mainCardsPanel.ResumeLayout();
        }

        private void UpdateCurrentRoomHeaderStats(RoomData room)
        {
            if (room == null) return;
            string noteDisplay = L(room.Note);
            lblContentTitle.Text = room.Name + (string.IsNullOrEmpty(noteDisplay) ? "" : $" ({noteDisplay})");
            int variety = room.Items.Count(i => store.Products.Any(p => p.Id == i.ProductId));
            int total = room.Items.Where(i => store.Products.Any(p => p.Id == i.ProductId)).Sum(i => i.Quantity);
            lblContentStats.Text = IsEN
                ? $"{variety} Product Types  •  Total {total} Counted"
                : $"{variety} Çeşit Ürün  •  Toplam {total} Adet Sayıldı";
        }

        private void AdjustListCardWidths()
        {
            if (store.ViewMode != "list") return;
            int targetWidth = Math.Max(520, mainCardsPanel.ClientSize.Width - 28);
            foreach (Control c in mainCardsPanel.Controls)
            {
                if (c.Tag as string == "list-card")
                {
                    c.Width = targetWidth;
                }
            }
        }

        // =====================================================================
        // VIEW 1: FLAT LIST CARD (SERIAL TABLE ONLY FOR LAPTOPS)
        // =====================================================================
        private Control CreateRoomProductListCard(RoomData room, ProductItem product, int quantity, int width)
        {
            var serials = room.GetSerials(product.Id);
            int serialCount = serials.Count;
            bool isLaptop = AppHelper.IsLaptopProduct(product.Name) || serialCount > 0;

            Panel card = new Panel
            {
                Tag = "list-card",
                Size = new Size(width, (isLaptop && serialCount > 0) ? 96 : 84),
                BackColor = ColCard,
                Margin = new Padding(2, 4, 2, 4)
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            PictureBox pic = CreateProductPictureBox(product, new Size(64, 64), new Point(10, 10));
            card.Controls.Add(pic);

            Label lblName = new Label
            {
                Text = L(product.Name),
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(84, 10),
                Size = new Size(width - 330, 24),
                AutoEllipsis = true
            };
            card.Controls.Add(lblName);

            int btnY = 42;
            if (isLaptop && serialCount > 0)
            {
                string preview = string.Join("  |  ", serials.Take(4).Select((sr, i) => $"#{i + 1} {sr.SerialNo} ({L(sr.Status)})"));
                Label lblSerialPreview = new Label
                {
                    Text = preview,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = ColSecondary,
                    Location = new Point(84, 34),
                    Size = new Size(width - 310, 18),
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };
                lblSerialPreview.Click += (s, e) => OpenLaptopSerialTableDialog(room, product);
                card.Controls.Add(lblSerialPreview);
                btnY = 58;
            }

            int nextBtnX = 84;

            if (isLaptop)
            {
                Button btnSerialTable = new Button
                {
                    Text = serialCount > 0
                        ? T($"Seri No Tablosu ({serialCount})", $"Serial No Table ({serialCount})")
                        : T("Seri No Tablosu", "Serial No Table"),
                    Location = new Point(nextBtnX, btnY),
                    Size = new Size(155, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = serialCount > 0 ? ColSecondary : ColPrimarySoft,
                    ForeColor = serialCount > 0 ? ColSecondaryFore : ColTextDark,
                    Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnSerialTable.FlatAppearance.BorderSize = 0;
                btnSerialTable.Click += (s, e) => OpenLaptopSerialTableDialog(room, product);
                card.Controls.Add(btnSerialTable);
                nextBtnX += 162;
            }

            Button btnEditProd = new Button
            {
                Text = T("Düzenle", "Edit"),
                Location = new Point(nextBtnX, btnY),
                Size = new Size(80, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColSurfaceAlt,
                ForeColor = ColTextDark,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnEditProd.FlatAppearance.BorderSize = 0;
            btnEditProd.Click += (s, e) => OpenProductEditDialog(product);

            Button btnRemoveFromRoom = new Button
            {
                Text = T("Kaldır", "Remove"),
                Location = new Point(nextBtnX + 86, btnY),
                Size = new Size(76, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColDangerBg,
                ForeColor = ColDanger,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRemoveFromRoom.FlatAppearance.BorderSize = 0;
            btnRemoveFromRoom.Click += (s, e) => RemoveProductFromCurrentRoom(room, product);

            Panel qtyControlBox = CreateQuantityControlBar(room, product, quantity, new Point(width - 196, (card.Height - 42) / 2));

            card.Resize += (s, e) =>
            {
                lblName.Width = Math.Max(150, card.Width - 310);
                qtyControlBox.Location = new Point(card.Width - 196, (card.Height - 42) / 2);
            };

            card.Controls.Add(btnEditProd);
            card.Controls.Add(btnRemoveFromRoom);
            card.Controls.Add(qtyControlBox);

            return card;
        }

        // =====================================================================
        // VIEW 2: SQUARE GRID CARD (SERIAL TABLE ONLY FOR LAPTOPS)
        // =====================================================================
        private Control CreateRoomProductGridCard(RoomData room, ProductItem product, int quantity)
        {
            var serials = room.GetSerials(product.Id);
            int serialCount = serials.Count;
            bool isLaptop = AppHelper.IsLaptopProduct(product.Name) || serialCount > 0;

            Panel card = new Panel
            {
                Size = new Size(220, isLaptop ? 304 : 272),
                BackColor = ColCard,
                Margin = new Padding(6)
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            PictureBox pic = CreateProductPictureBox(product, new Size(200, 140), new Point(10, 10));
            card.Controls.Add(pic);

            Label lblName = new Label
            {
                Text = L(product.Name),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(10, 155),
                Size = new Size(200, 24),
                AutoEllipsis = true
            };
            card.Controls.Add(lblName);

            int actionRowY = 184;

            if (isLaptop)
            {
                Button btnSerialTable = new Button
                {
                    Text = serialCount > 0
                        ? T($"Seri No Tablosu ({serialCount})", $"Serial No Table ({serialCount})")
                        : T("Seri No Tablosu", "Serial No Table"),
                    Location = new Point(10, 182),
                    Size = new Size(200, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = serialCount > 0 ? ColSecondary : ColPrimarySoft,
                    ForeColor = serialCount > 0 ? ColSecondaryFore : ColTextDark,
                    Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnSerialTable.FlatAppearance.BorderSize = 0;
                btnSerialTable.Click += (s, e) => OpenLaptopSerialTableDialog(room, product);
                card.Controls.Add(btnSerialTable);
                actionRowY = 215;
            }

            Button btnEdit = new Button
            {
                Text = T("Düzenle", "Edit"),
                Location = new Point(10, actionRowY),
                Size = new Size(97, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColSurfaceAlt,
                ForeColor = ColTextDark,
                Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnEdit.FlatAppearance.BorderSize = 0;
            btnEdit.Click += (s, e) => OpenProductEditDialog(product);

            Button btnDel = new Button
            {
                Text = T("Kaldır", "Remove"),
                Location = new Point(113, actionRowY),
                Size = new Size(97, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColDangerBg,
                ForeColor = ColDanger,
                Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDel.FlatAppearance.BorderSize = 0;
            btnDel.Click += (s, e) => RemoveProductFromCurrentRoom(room, product);

            Panel qtyBar = CreateQuantityControlBar(room, product, quantity, new Point(20, actionRowY + 33));

            card.Controls.Add(btnEdit);
            card.Controls.Add(btnDel);
            card.Controls.Add(qtyBar);

            return card;
        }

        // =====================================================================
        // QUANTITY CONTROL BAR: [ - ] [ INPUT ] [ + ]
        // =====================================================================
        private Panel CreateQuantityControlBar(RoomData room, ProductItem product, int initialQty, Point location)
        {
            Panel box = new Panel
            {
                Location = location,
                Size = new Size(180, 42),
                BackColor = ColInputBg
            };
            box.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, box.Width - 1, box.Height - 1);
            };

            Button btnMinus = new Button
            {
                Text = "−",
                Location = new Point(4, 4),
                Size = new Size(42, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColSurfaceAlt,
                ForeColor = ColTextDark,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMinus.FlatAppearance.BorderColor = ColBorder;

            TextBox txtQty = new TextBox
            {
                Text = initialQty.ToString(),
                Location = new Point(51, 7),
                Size = new Size(78, 28),
                TextAlign = HorizontalAlignment.Center,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                BackColor = ColCard,
                ForeColor = ColTextDark,
                BorderStyle = BorderStyle.FixedSingle
            };

            Button btnPlus = new Button
            {
                Text = "+",
                Location = new Point(134, 4),
                Size = new Size(42, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColPrimary,
                ForeColor = ColPrimaryFore,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPlus.FlatAppearance.BorderSize = 0;

            bool suppressTextEvent = false;

            txtQty.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };

            txtQty.TextChanged += (s, e) =>
            {
                if (suppressTextEvent) return;
                int parsed = 0;
                if (!string.IsNullOrWhiteSpace(txtQty.Text))
                    int.TryParse(txtQty.Text.Trim(), out parsed);
                if (parsed < 0) parsed = 0;

                room.SetQuantity(product.Id, parsed);
                AppHelper.SaveStore(store);
                UpdateCurrentRoomHeaderStats(room);
                RenderRoomsSidebar();
            };

            txtQty.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtQty.Text))
                {
                    suppressTextEvent = true;
                    txtQty.Text = room.GetQuantity(product.Id).ToString();
                    suppressTextEvent = false;
                }
            };

            btnMinus.Click += (s, e) =>
            {
                int current = room.GetQuantity(product.Id);
                int next = Math.Max(0, current - 1);
                room.SetQuantity(product.Id, next);
                suppressTextEvent = true;
                txtQty.Text = next.ToString();
                suppressTextEvent = false;
                AppHelper.SaveStore(store);
                UpdateCurrentRoomHeaderStats(room);
                RenderRoomsSidebar();
            };

            btnPlus.Click += (s, e) =>
            {
                int current = room.GetQuantity(product.Id);
                int next = current + 1;
                room.SetQuantity(product.Id, next);
                suppressTextEvent = true;
                txtQty.Text = next.ToString();
                suppressTextEvent = false;
                AppHelper.SaveStore(store);
                UpdateCurrentRoomHeaderStats(room);
                RenderRoomsSidebar();
            };

            box.Controls.Add(btnMinus);
            box.Controls.Add(txtQty);
            box.Controls.Add(btnPlus);
            return box;
        }

        private PictureBox CreateProductPictureBox(ProductItem product, Size size, Point location)
        {
            PictureBox pic = new PictureBox
            {
                Size = size,
                Location = location,
                BackColor = Color.White,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand
            };

            Image img = AppHelper.GetProductImage(product);
            if (img != null)
            {
                pic.Image = img;
                pic.Click += (s, e) => ShowPhotoLightbox(product);
            }
            else
            {
                pic.BackColor = ColSurfaceAlt;
                pic.Paint += (s, e) =>
                {
                    using (Font f = new Font("Segoe UI", 8f, FontStyle.Bold))
                    using (SolidBrush b = new SolidBrush(ColTextMuted))
                    {
                        StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        e.Graphics.DrawString(T("Fotoğraf\nEkle", "Add\nPhoto"), f, b, new RectangleF(0, 0, pic.Width, pic.Height), sf);
                    }
                };
                pic.Click += (s, e) => OpenProductEditDialog(product);
            }

            return pic;
        }

        // =====================================================================
        // CATALOG & SUMMARY CARDS
        // =====================================================================
        private Control CreateCatalogSummaryCardList(ProductItem prod, int width, bool isCatalogMode)
        {
            int totalCount = store.Rooms.Sum(r => r.GetQuantity(prod.Id));
            var roomsBreakdown = store.Rooms
                .Where(r => r.GetQuantity(prod.Id) > 0)
                .Select(r => $"{r.Name}: {r.GetQuantity(prod.Id)}")
                .ToList();

            bool isLaptop = AppHelper.IsLaptopProduct(prod.Name);
            var serialsInRooms = isLaptop
                ? store.Rooms
                    .Where(r => r.GetSerials(prod.Id).Count > 0)
                    .Select(r => $"{r.Name} ({string.Join(", ", r.GetSerials(prod.Id).Select(sr => sr.SerialNo + ":" + L(sr.Status)))})")
                    .ToList()
                : new List<string>();

            Panel card = new Panel
            {
                Tag = "list-card",
                Size = new Size(width, serialsInRooms.Count > 0 ? 96 : 82),
                BackColor = ColCard,
                Margin = new Padding(2, 4, 2, 4)
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            PictureBox pic = CreateProductPictureBox(prod, new Size(62, 62), new Point(10, 10));
            card.Controls.Add(pic);

            string breakdownText = roomsBreakdown.Count > 0
                ? T("Oda Dağılımı: ", "Rooms: ") + string.Join("  |  ", roomsBreakdown)
                : T("Henüz hiçbir odada sayılmadı (0)", "Not counted in any room (0)");

            Label lblName = new Label
            {
                Text = L(prod.Name),
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(84, 12),
                Size = new Size(Math.Max(160, width - 410), 24),
                AutoEllipsis = true
            };

            Label lblBreakdown = new Label
            {
                Text = breakdownText,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Regular),
                ForeColor = ColTextMuted,
                Location = new Point(84, 38),
                Size = new Size(Math.Max(160, width - 410), 20),
                AutoEllipsis = true
            };

            if (serialsInRooms.Count > 0)
            {
                Label lblNotes = new Label
                {
                    Text = string.Join("  |  ", serialsInRooms),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = ColSecondary,
                    Location = new Point(84, 62),
                    Size = new Size(Math.Max(160, width - 410), 20),
                    AutoEllipsis = true
                };
                card.Controls.Add(lblNotes);
            }

            Label lblTotalBadge = new Label
            {
                Text = IsEN ? $"Total: {totalCount}" : $"Toplam: {totalCount} Adet",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ColSecondary,
                Location = new Point(width - 315, 26),
                Size = new Size(165, 28),
                TextAlign = ContentAlignment.MiddleRight
            };

            card.Controls.Add(lblName);
            card.Controls.Add(lblBreakdown);
            card.Controls.Add(lblTotalBadge);

            if (isCatalogMode)
            {
                Button btnEdit = new Button
                {
                    Text = T("Düzenle", "Edit"),
                    Location = new Point(width - 140, 24),
                    Size = new Size(80, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColSurfaceAlt,
                    ForeColor = ColTextDark,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnEdit.FlatAppearance.BorderSize = 0;
                btnEdit.Click += (s, e) => OpenProductEditDialog(prod);

                Button btnDel = new Button
                {
                    Text = T("Sil", "Del"),
                    Location = new Point(width - 54, 24),
                    Size = new Size(44, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColDangerBg,
                    ForeColor = ColDanger,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnDel.FlatAppearance.BorderSize = 0;
                btnDel.Click += (s, e) => DeleteProductCompletely(prod);

                card.Resize += (s, e) =>
                {
                    lblName.Width = Math.Max(160, card.Width - 410);
                    lblBreakdown.Width = Math.Max(160, card.Width - 410);
                    lblTotalBadge.Location = new Point(card.Width - 315, 26);
                    btnEdit.Location = new Point(card.Width - 140, 24);
                    btnDel.Location = new Point(card.Width - 54, 24);
                };

                card.Controls.Add(btnEdit);
                card.Controls.Add(btnDel);
            }
            else
            {
                lblTotalBadge.Location = new Point(width - 185, 26);
                card.Resize += (s, e) =>
                {
                    lblName.Width = Math.Max(160, card.Width - 290);
                    lblBreakdown.Width = Math.Max(160, card.Width - 290);
                    lblTotalBadge.Location = new Point(card.Width - 185, 26);
                };
            }

            return card;
        }

        private Control CreateCatalogSummaryCardGrid(ProductItem prod, bool isCatalogMode)
        {
            Panel card = new Panel
            {
                Size = new Size(218, 268),
                BackColor = ColCard,
                Margin = new Padding(6)
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            PictureBox pic = CreateProductPictureBox(prod, new Size(198, 145), new Point(10, 10));
            card.Controls.Add(pic);

            int totalCount = store.Rooms.Sum(r => r.GetQuantity(prod.Id));

            Label lblName = new Label
            {
                Text = L(prod.Name),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(10, 162),
                Size = new Size(198, 38),
                AutoEllipsis = true
            };

            Label lblTotal = new Label
            {
                Text = IsEN ? $"Grand Total: {totalCount}" : $"Genel Toplam: {totalCount} Adet",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = ColSecondary,
                Location = new Point(10, 202),
                AutoSize = true
            };

            card.Controls.Add(lblName);
            card.Controls.Add(lblTotal);

            if (isCatalogMode)
            {
                Button btnEdit = new Button
                {
                    Text = T("Düzenle", "Edit"),
                    Location = new Point(10, 228),
                    Size = new Size(138, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColSurfaceAlt,
                    ForeColor = ColTextDark,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnEdit.FlatAppearance.BorderSize = 0;
                btnEdit.Click += (s, e) => OpenProductEditDialog(prod);

                Button btnDel = new Button
                {
                    Text = T("Sil", "Del"),
                    Location = new Point(154, 228),
                    Size = new Size(54, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColDangerBg,
                    ForeColor = ColDanger,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnDel.FlatAppearance.BorderSize = 0;
                btnDel.Click += (s, e) => DeleteProductCompletely(prod);

                card.Controls.Add(btnEdit);
                card.Controls.Add(btnDel);
            }
            else
            {
                var roomsBreakdown = store.Rooms
                    .Where(r => r.GetQuantity(prod.Id) > 0)
                    .Select(r => $"{r.Name}: {r.GetQuantity(prod.Id)}")
                    .ToList();

                Label lblBreak = new Label
                {
                    Text = roomsBreakdown.Count > 0 ? string.Join(", ", roomsBreakdown) : T("Odalarda yok", "Not in rooms"),
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = ColTextMuted,
                    Location = new Point(10, 226),
                    Size = new Size(198, 32),
                    AutoEllipsis = true
                };
                card.Controls.Add(lblBreak);
            }

            return card;
        }

        private Panel CreateEmptyStatePanel(string title, string desc)
        {
            Panel p = new Panel
            {
                Tag = "list-card",
                Size = new Size(Math.Max(520, mainCardsPanel.ClientSize.Width - 28), 160),
                BackColor = ColCard,
                Margin = new Padding(4)
            };
            p.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(ColBorder, 1))
                {
                    pen.DashStyle = DashStyle.Dash;
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
                }
            };

            Label lblT = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(20, 40),
                AutoSize = true
            };

            Label lblD = new Label
            {
                Text = desc,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ColTextMuted,
                Location = new Point(20, 72),
                Size = new Size(p.Width - 40, 60)
            };

            p.Controls.Add(lblT);
            p.Controls.Add(lblD);
            return p;
        }

        private void OpenLaptopSerialTableDialog(RoomData room, ProductItem product)
        {
            using (var dlg = new LaptopSerialTableDialog(store, room, product))
            {
                dlg.ShowDialog(this);
                AppHelper.SaveStore(store);
                RefreshAll();
            }
        }

        private void OnPrimaryActionClicked()
        {
            if (activeTab == "rooms")
            {
                var room = store.Rooms.FirstOrDefault(r => r.Id == currentRoomId);
                if (room == null) return;

                using (var dlg = new AddProductToRoomDialog(store, room))
                {
                    dlg.ShowDialog(this);
                    AppHelper.SaveStore(store);
                    RefreshAll();
                }
            }
            else if (activeTab == "catalog")
            {
                OpenProductEditDialog(null);
            }
        }

        private void OpenRoomDialog(RoomData existingRoom)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = existingRoom == null ? T("Yeni Oda Ekle", "Add New Room") : T("Oda Bilgilerini Düzenle", "Edit Room");
                dlg.Size = new Size(420, 265);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = ColCard;
                dlg.ForeColor = ColTextDark;
                dlg.Font = new Font("Segoe UI", 9.5f);
                dlg.HandleCreated += (s, e) => AppHelper.ApplyWindowTitleBarTheme(dlg, store.DarkMode);

                Label lbl1 = new Label { Text = T("Oda / Bölüm Adı *", "Room / Location Name *"), Location = new Point(20, 20), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                TextBox txtName = new TextBox { Location = new Point(20, 44), Width = 360, BackColor = ColInputBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Text = existingRoom != null ? existingRoom.Name : "" };

                Label lbl2 = new Label { Text = T("Açıklama (İsteğe Bağlı)", "Note / Description (Optional)"), Location = new Point(20, 86), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                TextBox txtNote = new TextBox { Location = new Point(20, 110), Width = 360, BackColor = ColInputBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Text = existingRoom != null ? L(existingRoom.Note) : "" };

                Button btnSave = new Button
                {
                    Text = T("Kaydet", "Save"),
                    Location = new Point(265, 165),
                    Size = new Size(115, 38),
                    BackColor = ColPrimary,
                    ForeColor = ColPrimaryFore,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnSave.FlatAppearance.BorderSize = 0;

                btnSave.Click += (s, e) =>
                {
                    string name = txtName.Text.Trim();
                    if (string.IsNullOrEmpty(name)) return;

                    if (existingRoom != null)
                    {
                        existingRoom.Name = name;
                        existingRoom.Note = txtNote.Text.Trim();
                    }
                    else
                    {
                        var newRoom = new RoomData
                        {
                            Id = "r_" + DateTime.Now.Ticks,
                            Name = name,
                            Note = txtNote.Text.Trim()
                        };
                        store.Rooms.Add(newRoom);
                        currentRoomId = newRoom.Id;
                    }

                    AppHelper.SaveStore(store);
                    RefreshAll();
                    dlg.DialogResult = DialogResult.OK;
                };

                dlg.Controls.AddRange(new Control[] { lbl1, txtName, lbl2, txtNote, btnSave });
                dlg.AcceptButton = btnSave;
                dlg.ShowDialog(this);
            }
        }

        private void DeleteCurrentRoom()
        {
            var room = store.Rooms.FirstOrDefault(r => r.Id == currentRoomId);
            if (room == null) return;

            var res = MessageBox.Show(
                T($"\"{room.Name}\" odasını silmek istediğinize emin misiniz?", $"Are you sure you want to delete room \"{room.Name}\"?"),
                T("Odayı Sil", "Delete Room"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                store.Rooms.Remove(room);
                currentRoomId = store.Rooms.FirstOrDefault()?.Id;
                AppHelper.SaveStore(store);
                RefreshAll();
            }
        }

        private void RemoveProductFromCurrentRoom(RoomData room, ProductItem product)
        {
            var res = MessageBox.Show(
                T($"\"{L(product.Name)}\" ürününü bu odadan kaldırmak istiyor musunuz?", $"Remove \"{L(product.Name)}\" from this room?"),
                T("Odadan Kaldır", "Remove Item"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                room.RemoveProduct(product.Id);
                AppHelper.SaveStore(store);
                RefreshAll();
            }
        }

        private void DeleteProductCompletely(ProductItem product)
        {
            var res = MessageBox.Show(
                T($"\"{L(product.Name)}\" ürününü tüm odalardan silmek istediğinize emin misiniz?", $"Delete \"{L(product.Name)}\" from catalog and all rooms?"),
                T("Ürünü Sil", "Delete Product"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                store.Products.RemoveAll(p => p.Id == product.Id);
                foreach (var r in store.Rooms)
                    r.RemoveProduct(product.Id);

                AppHelper.ClearImageCache(product.Id);
                AppHelper.SaveStore(store);
                RefreshAll();
            }
        }

        private void OpenProductEditDialog(ProductItem existingProduct)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = existingProduct == null ? T("Havuza Yeni Ürün Tanımla", "Add New Product") : T("Ürün Adı ve Fotoğrafını Düzenle", "Edit Product Name & Photo");
                dlg.Size = new Size(460, 360);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = ColCard;
                dlg.ForeColor = ColTextDark;
                dlg.Font = new Font("Segoe UI", 9.5f);
                dlg.HandleCreated += (s, e) => AppHelper.ApplyWindowTitleBarTheme(dlg, store.DarkMode);

                string tempPhotoBase64 = existingProduct != null ? existingProduct.PhotoBase64 : null;

                Label lblName = new Label { Text = T("Ürün Adı *", "Product Name *"), Location = new Point(20, 18), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                TextBox txtName = new TextBox { Location = new Point(20, 42), Width = 400, BackColor = ColInputBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Text = existingProduct != null ? L(existingProduct.Name) : "" };

                Label lblPhoto = new Label { Text = T("Ürün Fotoğrafı", "Product Photo"), Location = new Point(20, 84), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

                PictureBox picPreview = new PictureBox
                {
                    Location = new Point(20, 110),
                    Size = new Size(125, 125),
                    BackColor = Color.White,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle
                };

                if (existingProduct != null)
                {
                    Image existingImg = AppHelper.GetProductImage(existingProduct);
                    if (existingImg != null) picPreview.Image = existingImg;
                }

                Button btnPickPhoto = new Button
                {
                    Text = T("Bilgisayardan Fotoğraf Seç...", "Choose Photo from PC..."),
                    Location = new Point(160, 110),
                    Size = new Size(260, 38),
                    BackColor = ColPrimarySoft,
                    ForeColor = ColTextDark,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnPickPhoto.FlatAppearance.BorderSize = 0;

                Button btnClearPhoto = new Button
                {
                    Text = T("Fotoğrafı Kaldır", "Remove Photo"),
                    Location = new Point(160, 156),
                    Size = new Size(140, 32),
                    BackColor = ColSurfaceAlt,
                    ForeColor = ColTextMuted,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5f)
                };
                btnClearPhoto.FlatAppearance.BorderSize = 0;

                btnPickPhoto.Click += (s, e) =>
                {
                    using (OpenFileDialog ofd = new OpenFileDialog())
                    {
                        ofd.Title = T("Fotoğraf Seç", "Choose Photo");
                        ofd.Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
                        if (ofd.ShowDialog(dlg) == DialogResult.OK)
                        {
                            try
                            {
                                tempPhotoBase64 = AppHelper.LoadAndResizeImageToBase64(ofd.FileName);
                                byte[] bytes = Convert.FromBase64String(tempPhotoBase64);
                                using (var ms = new MemoryStream(bytes))
                                {
                                    picPreview.Image = new Bitmap(Image.FromStream(ms));
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message);
                            }
                        }
                    }
                };

                btnClearPhoto.Click += (s, e) =>
                {
                    tempPhotoBase64 = null;
                    picPreview.Image = null;
                };

                Button btnSave = new Button
                {
                    Text = T("Kaydet", "Save"),
                    Location = new Point(300, 260),
                    Size = new Size(120, 40),
                    BackColor = ColPrimary,
                    ForeColor = ColPrimaryFore,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnSave.FlatAppearance.BorderSize = 0;

                btnSave.Click += (s, e) =>
                {
                    string name = txtName.Text.Trim();
                    if (string.IsNullOrEmpty(name)) return;

                    if (existingProduct != null)
                    {
                        existingProduct.Name = name;
                        existingProduct.PhotoBase64 = tempPhotoBase64;
                        AppHelper.ClearImageCache(existingProduct.Id);
                    }
                    else
                    {
                        store.Products.Insert(0, new ProductItem
                        {
                            Id = "p_" + DateTime.Now.Ticks,
                            Name = name,
                            PhotoBase64 = tempPhotoBase64
                        });
                    }

                    AppHelper.SaveStore(store);
                    RefreshAll();
                    dlg.DialogResult = DialogResult.OK;
                };

                dlg.Controls.AddRange(new Control[] { lblName, txtName, lblPhoto, picPreview, btnPickPhoto, btnClearPhoto, btnSave });
                dlg.AcceptButton = btnSave;
                dlg.ShowDialog(this);
            }
        }

        private void ShowPhotoLightbox(ProductItem product)
        {
            Image img = AppHelper.GetProductImage(product);
            if (img == null) return;

            using (Form dlg = new Form())
            {
                dlg.Text = L(product.Name);
                dlg.Size = new Size(560, 560);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = ColBg;
                dlg.HandleCreated += (s, e) => AppHelper.ApplyWindowTitleBarTheme(dlg, store.DarkMode);

                PictureBox pic = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = img,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.White
                };
                dlg.Controls.Add(pic);
                dlg.ShowDialog(this);
            }
        }

        private void ExportToExcel()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = T("Excel Dosyasını Kaydet (.xlsx)", "Save Excel Workbook (.xlsx)");
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";
                sfd.FileName = $"Envanter_{DateTime.Now:yyyy-MM-dd}.xlsx";

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string ext = Path.GetExtension(sfd.FileName)?.ToLowerInvariant() ?? ".xlsx";
                        if (ext == ".csv")
                        {
                            // Matris formatında CSV dışa aktarma (Room;Ürün1;Ürün2;...)
                            StringBuilder sb = new StringBuilder();
                            var headerCells = new List<string> { "Room" };
                            headerCells.AddRange(store.Products.Select(p => $"\"{EscapeCsv(L(p.Name))}\""));
                            sb.AppendLine(string.Join(";", headerCells));

                            foreach (var room in store.Rooms)
                            {
                                var rowCells = new List<string> { $"\"{EscapeCsv(room.Name)}\"" };
                                foreach (var p in store.Products)
                                {
                                    int q = room.GetQuantity(p.Id);
                                    rowCells.Add(q > 0 ? q.ToString() : "");
                                }
                                sb.AppendLine(string.Join(";", rowCells));
                            }
                            File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                        }
                        else
                        {
                            ExcelHelper.ExportToXlsx(store, sfd.FileName);
                        }

                        MessageBox.Show(
                            T("Excel dosyası başarıyla kaydedildi!\n\n• 1. Sekme (Envanter): Oda x Ürün matris tablosu\n• 2. Sekme (Laptop_Seri_Nolar): PC/Laptop seri no & durum giriş tablosu\n\nBu dosyayı Excel'de düzenleyip 'Excel Yükle' ile geri içeri aktarabilirsiniz.",
                              "Excel workbook saved successfully!\n\n• Sheet 1 (Inventory): Room x Product matrix table\n• Sheet 2 (Laptop_Serials): PC/Laptop serial no & status table\n\nYou can edit it in Excel and import it right back using 'Import Excel'."),
                            T("Excel Kaydedildi", "Excel Saved"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, T("Hata", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ImportFromExcel()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = T("Excel Dosyasından Veri Yükle (.xlsx / .csv)", "Import Data from Excel (.xlsx / .csv)");
                ofd.Filter = "Excel & CSV (*.xlsx;*.csv)|*.xlsx;*.csv|Excel Workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    var modeChoice = MessageBox.Show(
                        T("Excel verileri nasıl yüklensin?\n\n• EVET = Mevcut odaların üzerine ekle / güncelle (Birleştir)\n• HAYIR = Mevcut odaları temizle, sadece bu Excel'deki odaları yükle (Ürün fotoğrafları korunur)\n• İPTAL = Vazgeç",
                          "How would you like to import Excel data?\n\n• YES = Merge / Update existing rooms\n• NO = Clear existing rooms and load only from this Excel (Product photos are preserved)\n• CANCEL = Abort"),
                        T("Excel İçe Aktarma", "Import from Excel"),
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (modeChoice == DialogResult.Cancel) return;
                    bool replaceRooms = (modeChoice == DialogResult.No);

                    try
                    {
                        var res = ExcelHelper.ImportFromExcelOrCsv(store, ofd.FileName, replaceRooms);
                        if (store.Rooms.Count > 0 && (currentRoomId == null || !store.Rooms.Any(r => r.Id == currentRoomId)))
                        {
                            currentRoomId = store.Rooms[0].Id;
                        }
                        AppHelper.SaveStore(store);
                        RefreshAll();

                        MessageBox.Show(
                            T($"Excel verileri başarıyla yüklendi!\n\n• İşlenen Oda Sayısı: {res.RoomsImported}\n• Yeni Eklenen Ürün Çeşidi: {res.ProductsAdded}\n• Güncellenen Ürün Hücresi: {res.CellEntriesUpdated}\n• Yüklenen Laptop Seri No: {res.SerialsImported}",
                              $"Excel data imported successfully!\n\n• Rooms Processed: {res.RoomsImported}\n• New Product Types Added: {res.ProductsAdded}\n• Product Cells Updated: {res.CellEntriesUpdated}\n• Laptop Serial Numbers Imported: {res.SerialsImported}"),
                            T("İçe Aktarma Tamamlandı", "Import Completed"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            T("Excel dosyası okunurken hata oluştu:\n", "Error reading Excel file:\n") + ex.Message,
                            T("Hata", "Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void OpenLaptopExcelDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = T("Laptop Excel Listesi", "Laptop Excel List");
                dlg.Size = new Size(460, 268);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = ColCard;
                dlg.ForeColor = ColTextDark;
                dlg.Font = new Font("Segoe UI", 9.5f);
                dlg.HandleCreated += (s, e) => AppHelper.ApplyWindowTitleBarTheme(dlg, store.DarkMode);

                Label lblTitle = new Label
                {
                    Text = T("Laptoplar İçin Ayrı Excel İçe / Dışa Aktar", "Separate Excel Import / Export for Laptops"),
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(20, 18),
                    Size = new Size(410, 26)
                };

                Label lblDesc = new Label
                {
                    Text = T("Sadece laptopların oda, model, seri no ve durum listesini ayrı bir Excel (.xlsx / .csv) olarak dışa aktarabilir veya geri yükleyebilirsiniz.",
                             "Export or import only your laptops (room, model, serial no & status) as a dedicated Excel (.xlsx / .csv) list without affecting other items."),
                    Font = new Font("Segoe UI", 8.8f),
                    ForeColor = ColTextMuted,
                    Location = new Point(20, 48),
                    Size = new Size(406, 44)
                };

                Label lblFormat = new Label
                {
                    Text = T("Format:  Room  |  Laptop  |  NO  |  SERİ NO  |  DURUM", "Format:  Room  |  Laptop  |  NO  |  SERIAL NO  |  STATUS"),
                    Font = new Font("Consolas", 8.8f, FontStyle.Bold),
                    ForeColor = ColSecondary,
                    BackColor = ColBg,
                    Location = new Point(20, 98),
                    Size = new Size(404, 30),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Button btnLapExport = new Button
                {
                    Text = T("Laptop Excel İndir (.xlsx)", "Export Laptop Excel (.xlsx)"),
                    Location = new Point(20, 146),
                    Size = new Size(196, 42),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColPrimary,
                    ForeColor = ColPrimaryFore,
                    Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnLapExport.FlatAppearance.BorderSize = 0;
                btnLapExport.Click += (s, e) =>
                {
                    dlg.Close();
                    ExportLaptopsToExcel();
                };

                Button btnLapImport = new Button
                {
                    Text = T("Laptop Excel Yükle", "Import Laptop Excel"),
                    Location = new Point(228, 146),
                    Size = new Size(196, 42),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColSecondary,
                    ForeColor = ColSecondaryFore,
                    Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnLapImport.FlatAppearance.BorderSize = 0;
                btnLapImport.Click += (s, e) =>
                {
                    dlg.Close();
                    ImportLaptopsFromExcel();
                };

                Button btnClose = new Button
                {
                    Text = T("Kapat", "Close"),
                    Location = new Point(324, 198),
                    Size = new Size(100, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColSurfaceAlt,
                    ForeColor = ColTextDark,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Click += (s, e) => dlg.Close();

                dlg.Controls.AddRange(new Control[] { lblTitle, lblDesc, lblFormat, btnLapExport, btnLapImport, btnClose });
                dlg.ShowDialog(this);
            }
        }

        private void ExportLaptopsToExcel()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = T("Laptop Excel Listesini Kaydet (.xlsx)", "Save Laptop Excel List (.xlsx)");
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";
                sfd.FileName = $"Laptop_Listesi_{DateTime.Now:yyyy-MM-dd}.xlsx";

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string ext = Path.GetExtension(sfd.FileName)?.ToLowerInvariant() ?? ".xlsx";
                        if (ext == ".csv")
                            ExcelHelper.ExportLaptopsToCsv(store, sfd.FileName);
                        else
                            ExcelHelper.ExportLaptopsToXlsx(store, sfd.FileName);

                        MessageBox.Show(
                            T("Laptop listesi başarıyla Excel olarak kaydedildi!\n\nSütunlar: Room | Laptop | NO | SERİ NO | DURUM\n\nExcel'de seri numaralarını doldurup 'Laptop Excel → Yükle' ile geri içeri aktarabilirsiniz.",
                              "Laptop Excel list saved successfully!\n\nColumns: Room | Laptop | NO | SERIAL NO | STATUS\n\nFill in serial numbers in Excel and import it back via 'Laptop Excel → Import'."),
                            T("Laptop Excel Kaydedildi", "Laptop Excel Saved"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, T("Hata", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ImportLaptopsFromExcel()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = T("Laptop Excel Listesi Yükle (.xlsx / .csv)", "Import Laptop Excel List (.xlsx / .csv)");
                ofd.Filter = "Excel & CSV (*.xlsx;*.csv)|*.xlsx;*.csv|Excel Workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    var modeChoice = MessageBox.Show(
                        T("Laptop seri no listesi nasıl yüklensin?\n\n• EVET = Mevcut laptop seri numaralarının üzerine ekle / güncelle (Birleştir)\n• HAYIR = Mevcut laptop seri numaralarını temizle, sadece bu Excel'dekileri yükle (Diğer ürünlere dokunulmaz)\n• İPTAL = Vazgeç",
                          "How would you like to import the Laptop Excel list?\n\n• YES = Merge / Update existing laptop serial numbers\n• NO = Clear existing laptop serial numbers and load only from this Excel (Other items are untouched)\n• CANCEL = Abort"),
                        T("Laptop Excel Yükle", "Import Laptop Excel"),
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (modeChoice == DialogResult.Cancel) return;
                    bool replaceSerials = (modeChoice == DialogResult.No);

                    try
                    {
                        var res = ExcelHelper.ImportLaptopsFromExcelOrCsv(store, ofd.FileName, replaceSerials);
                        if (store.Rooms.Count > 0 && (currentRoomId == null || !store.Rooms.Any(r => r.Id == currentRoomId)))
                        {
                            currentRoomId = store.Rooms[0].Id;
                        }
                        AppHelper.SaveStore(store);
                        RefreshAll();

                        MessageBox.Show(
                            T($"Laptop Excel listesi başarıyla yüklendi!\n\n• İşlenen Oda Sayısı: {res.RoomsImported}\n• Yeni Eklenen Laptop Seri No: {res.SerialsImported}\n• Güncellenen Seri No: {res.SerialsUpdated}\n• Yeni Laptop Modeli: {res.ProductsAdded}",
                              $"Laptop Excel list imported successfully!\n\n• Rooms Processed: {res.RoomsImported}\n• New Serial Numbers Added: {res.SerialsImported}\n• Existing Serials Updated: {res.SerialsUpdated}\n• New Laptop Models Added: {res.ProductsAdded}"),
                            T("Laptop Excel Yüklendi", "Laptop Excel Imported"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            T("Laptop Excel dosyası okunurken hata oluştu:\n", "Error reading Laptop Excel file:\n") + ex.Message,
                            T("Hata", "Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }

        private string EscapeCsv(string val) => (val ?? "").Replace("\"", "\"\"");

        private void ResetAllData()
        {
            var res = MessageBox.Show(
                T("TÜM ODALARI VE ÜRÜNLERİ SİLMEK İSTEDİĞİNİZE EMİN MİSİNİZ?", "ARE YOU SURE YOU WANT TO DELETE ALL ROOMS AND PRODUCTS?"),
                T("Sıfırla", "Reset All"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (res == DialogResult.Yes)
            {
                AppHelper.ClearImageCache();
                store.Rooms.Clear();
                store.Products.Clear();
                currentRoomId = null;
                AppHelper.SaveStore(store);
                RefreshAll();
            }
        }
    }

    // =========================================================================
    // LAPTOP SERIAL TABLE DIALOG (NO | SERIAL NO | STATUS)
    // =========================================================================
    public class LaptopSerialTableDialog : Form
    {
        private readonly InventoryStore store;
        private readonly RoomData room;
        private readonly ProductItem product;
        private List<SerialRecord> serials;

        private string editingSerialId = null;
        private TextBox txtSerialNo;
        private TextBox txtStatus;
        private Button btnAddOrUpdate;
        private Button btnCancelEdit;
        private SmoothFlowPanel tableRowsPanel;
        private Label lblSubInfo;
        private Button btnSyncQty;

        private bool IsEN => store.Language == "en";
        private string T(string tr, string en) => IsEN ? en : tr;
        private string L(string raw) => AppHelper.LocalizeText(raw, IsEN);

        private Color ColPrimary => AppHelper.GetPrimaryColor(store.ColorTheme, store.DarkMode);
        private Color ColPrimaryFore => AppHelper.GetPrimaryForeColor(store.ColorTheme, store.DarkMode);
        private Color ColSecondary => AppHelper.GetSecondaryColor(store.ColorTheme, store.DarkMode);
        private Color ColSecondaryFore => AppHelper.GetSecondaryForeColor(store.ColorTheme, store.DarkMode);
        private Color ColBg => AppHelper.GetBgColor(store.ColorTheme, store.DarkMode);
        private Color ColCard => AppHelper.GetCardColor(store.ColorTheme, store.DarkMode);
        private Color ColSurfaceAlt => AppHelper.GetSurfaceAltColor(store.ColorTheme, store.DarkMode);
        private Color ColTextDark => store.DarkMode ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
        private Color ColBorder => AppHelper.GetBorderColor(store.ColorTheme, store.DarkMode);
        private Color ColDanger => store.DarkMode ? Color.FromArgb(251, 113, 133) : Color.FromArgb(220, 38, 38);
        private Color ColDangerBg => store.DarkMode ? Color.FromArgb(64, 14, 22) : Color.FromArgb(254, 226, 226);

        public LaptopSerialTableDialog(InventoryStore store, RoomData room, ProductItem product)
        {
            this.store = store;
            this.room = room;
            this.product = product;
            this.serials = room.GetSerials(product.Id);

            this.Text = T($"Laptop Seri No Tablosu — {product.Name}", $"Laptop Serial Number Table — {L(product.Name)}");
            this.Size = new Size(720, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = ColCard;
            this.ForeColor = ColTextDark;
            this.Font = new Font("Segoe UI", 9.5f);

            BuildTableUI();
            RenderTableRows();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AppHelper.ApplyWindowTitleBarTheme(this, store.DarkMode);
        }

        private void BuildTableUI()
        {
            Panel topInfo = new Panel
            {
                Location = new Point(18, 14),
                Size = new Size(668, 66),
                BackColor = ColBg
            };

            PictureBox pic = new PictureBox
            {
                Location = new Point(8, 5),
                Size = new Size(56, 56),
                BackColor = Color.White,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            Image img = AppHelper.GetProductImage(product);
            if (img != null) pic.Image = img;

            Label lblTitle = new Label
            {
                Text = $"{L(product.Name).ToUpperInvariant()} — " + T("SERİ NO & DURUM", "SERIAL NO & STATUS"),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(74, 10),
                Size = new Size(376, 24),
                AutoEllipsis = true
            };

            lblSubInfo = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                ForeColor = ColSecondary,
                Location = new Point(74, 36),
                Size = new Size(376, 20),
                AutoEllipsis = true
            };

            Button btnImportSingleLaptop = new Button
            {
                Text = T("Excel Yükle", "Import Excel"),
                Location = new Point(458, 17),
                Size = new Size(98, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColSurfaceAlt,
                ForeColor = ColTextDark,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnImportSingleLaptop.FlatAppearance.BorderColor = ColBorder;
            btnImportSingleLaptop.FlatAppearance.BorderSize = 1;
            btnImportSingleLaptop.Click += (s, e) => ImportSingleLaptopExcel();

            Button btnExportSingleLaptop = new Button
            {
                Text = T("Excel İndir", "Export Excel"),
                Location = new Point(562, 17),
                Size = new Size(98, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColSecondary,
                ForeColor = ColSecondaryFore,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnExportSingleLaptop.FlatAppearance.BorderSize = 0;
            btnExportSingleLaptop.Click += (s, e) => ExportSingleLaptopExcel();

            topInfo.Controls.AddRange(new Control[] { pic, lblTitle, lblSubInfo, btnImportSingleLaptop, btnExportSingleLaptop });

            Label lblSerialInput = new Label
            {
                Text = T("SERİ NO:", "SERIAL NO:"),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(18, 90),
                AutoSize = true
            };

            txtSerialNo = new TextBox
            {
                Location = new Point(18, 112),
                Size = new Size(235, 30),
                BackColor = ColBg,
                ForeColor = ColTextDark,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            Label lblStatusInput = new Label
            {
                Text = T("DURUM (Örn: Aktif, Depoda, Arızalı):", "STATUS (e.g. Active, In Stock, Broken):"),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(265, 90),
                AutoSize = true
            };

            txtStatus = new TextBox
            {
                Location = new Point(265, 112),
                Size = new Size(225, 30),
                BackColor = ColBg,
                ForeColor = ColTextDark,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f)
            };

            KeyEventHandler enterHandler = (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    SaveSerialRow();
                }
            };
            txtSerialNo.KeyDown += enterHandler;
            txtStatus.KeyDown += enterHandler;

            btnAddOrUpdate = new Button
            {
                Text = T("+ Tabloya Ekle", "+ Add Row"),
                Location = new Point(500, 110),
                Size = new Size(125, 32),
                BackColor = ColPrimary,
                ForeColor = ColPrimaryFore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAddOrUpdate.FlatAppearance.BorderSize = 0;
            btnAddOrUpdate.Click += (s, e) => SaveSerialRow();

            btnCancelEdit = new Button
            {
                Text = T("İptal", "Cancel"),
                Location = new Point(630, 110),
                Size = new Size(56, 32),
                BackColor = ColSurfaceAlt,
                ForeColor = ColTextDark,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Visible = false,
                Cursor = Cursors.Hand
            };
            btnCancelEdit.FlatAppearance.BorderSize = 0;
            btnCancelEdit.Click += (s, e) => CancelEditMode();

            Panel tableHeader = new Panel
            {
                Location = new Point(18, 154),
                Size = new Size(668, 38),
                BackColor = ColSurfaceAlt
            };
            tableHeader.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 2))
                {
                    e.Graphics.DrawRectangle(p, 1, 1, tableHeader.Width - 2, tableHeader.Height - 2);
                    e.Graphics.DrawLine(p, 65, 0, 65, tableHeader.Height);
                    e.Graphics.DrawLine(p, 325, 0, 325, tableHeader.Height);
                    e.Graphics.DrawLine(p, 535, 0, 535, tableHeader.Height);
                }
            };

            Label colNo = new Label { Text = "NO", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = ColTextDark, Location = new Point(16, 9), AutoSize = true };
            Label colSerial = new Label { Text = T("SERİ NO", "SERIAL NO"), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = ColTextDark, Location = new Point(78, 9), AutoSize = true };
            Label colStatus = new Label { Text = T("DURUM", "STATUS"), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = ColTextDark, Location = new Point(338, 9), AutoSize = true };
            Label colAct = new Label { Text = T("DÜZELT / SİL", "EDIT / DEL"), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = ColTextDark, Location = new Point(545, 10), AutoSize = true };

            tableHeader.Controls.AddRange(new Control[] { colNo, colSerial, colStatus, colAct });

            tableRowsPanel = new SmoothFlowPanel
            {
                Location = new Point(18, 192),
                Size = new Size(668, 345),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = ColBg,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnSyncQty = new Button
            {
                Text = "",
                Location = new Point(18, 550),
                Size = new Size(340, 40),
                BackColor = ColSecondary,
                ForeColor = ColSecondaryFore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSyncQty.FlatAppearance.BorderSize = 0;
            btnSyncQty.Click += (s, e) =>
            {
                room.SetQuantity(product.Id, serials.Count);
                AppHelper.SaveStore(store);
                UpdateCounters();
                MessageBox.Show(
                    T($"\"{L(product.Name)}\" adedi tablodaki seri no sayısına ({serials.Count}) eşitlendi.",
                      $"\"{L(product.Name)}\" quantity synced to {serials.Count}."),
                    T("Tamam", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnClose = new Button
            {
                Text = T("Kaydet ve Kapat", "Save & Close"),
                Location = new Point(526, 550),
                Size = new Size(160, 40),
                BackColor = ColPrimary,
                ForeColor = ColPrimaryFore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) =>
            {
                AppHelper.SaveStore(store);
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            this.Controls.AddRange(new Control[]
            {
                topInfo, lblSerialInput, txtSerialNo, lblStatusInput, txtStatus,
                btnAddOrUpdate, btnCancelEdit, tableHeader, tableRowsPanel,
                btnSyncQty, btnClose
            });
        }

        private void SaveSerialRow()
        {
            string sNo = (txtSerialNo.Text ?? "").Trim();
            string st = (txtStatus.Text ?? "").Trim();
            if (string.IsNullOrEmpty(sNo))
            {
                MessageBox.Show(T("Lütfen Seri No girin.", "Please enter a Serial Number."), T("Uyarı", "Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSerialNo.Focus();
                return;
            }
            if (string.IsNullOrEmpty(st))
                st = T("Aktif", "Active");

            if (editingSerialId != null)
            {
                var existing = serials.FirstOrDefault(x => x.Id == editingSerialId);
                if (existing != null)
                {
                    existing.SerialNo = sNo;
                    existing.Status = st;
                }
                CancelEditMode();
            }
            else
            {
                serials.Add(new SerialRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SerialNo = sNo,
                    Status = st
                });
                txtSerialNo.Text = "";
                txtStatus.Text = "";
                txtSerialNo.Focus();
            }

            AppHelper.SaveStore(store);
            RenderTableRows();
        }

        private void StartEditRow(SerialRecord rec)
        {
            editingSerialId = rec.Id;
            txtSerialNo.Text = rec.SerialNo;
            txtStatus.Text = L(rec.Status);
            btnAddOrUpdate.Text = T("Güncelle", "Update");
            btnAddOrUpdate.BackColor = ColSecondary;
            btnAddOrUpdate.ForeColor = ColSecondaryFore;
            btnCancelEdit.Visible = true;
            txtSerialNo.Focus();
            RenderTableRows();
        }

        private void CancelEditMode()
        {
            editingSerialId = null;
            txtSerialNo.Text = "";
            txtStatus.Text = "";
            btnAddOrUpdate.Text = T("+ Tabloya Ekle", "+ Add Row");
            btnAddOrUpdate.BackColor = ColPrimary;
            btnAddOrUpdate.ForeColor = ColPrimaryFore;
            btnCancelEdit.Visible = false;
            RenderTableRows();
        }

        private void DeleteRow(SerialRecord rec)
        {
            serials.RemoveAll(x => x.Id == rec.Id);
            if (editingSerialId == rec.Id)
                CancelEditMode();
            AppHelper.SaveStore(store);
            RenderTableRows();
        }

        private void ExportSingleLaptopExcel()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                string safeName = string.Concat(product.Name.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == ' ')).Trim().Replace(' ', '_');
                if (string.IsNullOrEmpty(safeName)) safeName = "Laptop";
                sfd.Title = T("Bu Laptopun Excel Listesini Kaydet", "Save This Laptop's Excel List");
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";
                sfd.FileName = $"{safeName}_SeriNolar_{DateTime.Now:yyyy-MM-dd}.xlsx";

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string ext = Path.GetExtension(sfd.FileName)?.ToLowerInvariant() ?? ".xlsx";
                        if (ext == ".csv")
                            ExcelHelper.ExportLaptopsToCsv(store, sfd.FileName, room, product);
                        else
                            ExcelHelper.ExportLaptopsToXlsx(store, sfd.FileName, room, product);

                        MessageBox.Show(
                            T($"\"{L(product.Name)}\" seri no listesi Excel olarak kaydedildi.", $"\"{L(product.Name)}\" serial list exported to Excel."),
                            T("Tamam", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, T("Hata", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ImportSingleLaptopExcel()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = T("Bu Laptop İçin Excel'den Seri No Yükle", "Import Serial Numbers for This Laptop");
                ofd.Filter = "Excel & CSV (*.xlsx;*.csv)|*.xlsx;*.csv|Excel Workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    var choice = MessageBox.Show(
                        T("Seri numaraları nasıl yüklensin?\n\n• EVET = Mevcut listeye ekle / güncelle\n• HAYIR = Bu laptopun mevcut listesini temizle, sadece Excel'dekileri yükle\n• İPTAL = Vazgeç",
                          "How should serial numbers be imported?\n\n• YES = Merge / update existing list\n• NO = Clear this laptop's current list and load only from Excel\n• CANCEL = Abort"),
                        T("Laptop Excel Yükle", "Import Laptop Excel"),
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (choice == DialogResult.Cancel) return;
                    bool replace = (choice == DialogResult.No);

                    try
                    {
                        var res = ExcelHelper.ImportLaptopsFromExcelOrCsv(store, ofd.FileName, replace, room, product);
                        this.serials = room.GetSerials(product.Id);
                        AppHelper.SaveStore(store);
                        RenderTableRows();

                        MessageBox.Show(
                            T($"Yükleme tamamlandı!\n\n• Yeni Eklenen Seri No: {res.SerialsImported}\n• Güncellenen: {res.SerialsUpdated}",
                              $"Import completed!\n\n• New Serials Added: {res.SerialsImported}\n• Updated: {res.SerialsUpdated}"),
                            T("Tamam", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, T("Hata", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void UpdateCounters()
        {
            lblSubInfo.Text = IsEN
                ? $"Room: {room.Name}   •   Room Quantity: {room.GetQuantity(product.Id)}   •   Recorded Serials: {serials.Count}"
                : $"Oda: {room.Name}   •   Oda Sayım Adedi: {room.GetQuantity(product.Id)}   •   Tablodaki Seri No: {serials.Count}";
            btnSyncQty.Text = IsEN
                ? $"Sync Item Quantity to Serial Count ({serials.Count})"
                : $"Ürün Adedini Seri No Sayısına Eşitle ({serials.Count})";
        }

        private void RenderTableRows()
        {
            UpdateCounters();
            tableRowsPanel.SuspendLayout();
            foreach (Control c in tableRowsPanel.Controls) c.Dispose();
            tableRowsPanel.Controls.Clear();

            for (int i = 0; i < serials.Count; i++)
            {
                var rec = serials[i];
                int rowNo = i + 1;
                bool isEditingThis = (rec.Id == editingSerialId);

                Panel row = new Panel
                {
                    Size = new Size(644, 42),
                    BackColor = isEditingThis ? AppHelper.GetSecondarySoftBg(store.ColorTheme, store.DarkMode) : ColCard,
                    Margin = new Padding(0)
                };
                row.Paint += (s, e) =>
                {
                    using (Pen p = new Pen(ColBorder, 1))
                    {
                        e.Graphics.DrawLine(p, 0, row.Height - 1, row.Width, row.Height - 1);
                        e.Graphics.DrawLine(p, 65, 0, 65, row.Height);
                        e.Graphics.DrawLine(p, 325, 0, 325, row.Height);
                        e.Graphics.DrawLine(p, 535, 0, 535, row.Height);
                    }
                };

                Label lblNo = new Label
                {
                    Text = rowNo.ToString(),
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = ColSecondary,
                    Location = new Point(6, 10),
                    Size = new Size(52, 22),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Label lblSerial = new Label
                {
                    Text = rec.SerialNo,
                    Font = new Font("Consolas", 10.5f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(74, 11),
                    Size = new Size(242, 22),
                    AutoEllipsis = true
                };

                Label lblStatus = new Label
                {
                    Text = L(rec.Status),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                    ForeColor = ColTextDark,
                    Location = new Point(334, 10),
                    Size = new Size(192, 22),
                    AutoEllipsis = true
                };

                Button btnEdit = new Button
                {
                    Text = T("Düzelt", "Edit"),
                    Location = new Point(542, 7),
                    Size = new Size(56, 28),
                    BackColor = ColSurfaceAlt,
                    ForeColor = ColTextDark,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnEdit.FlatAppearance.BorderSize = 0;
                btnEdit.Click += (s, e) => StartEditRow(rec);

                Button btnDel = new Button
                {
                    Text = T("Sil", "Del"),
                    Location = new Point(602, 7),
                    Size = new Size(38, 28),
                    BackColor = ColDangerBg,
                    ForeColor = ColDanger,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnDel.FlatAppearance.BorderSize = 0;
                btnDel.Click += (s, e) => DeleteRow(rec);

                row.Controls.AddRange(new Control[] { lblNo, lblSerial, lblStatus, btnEdit, btnDel });
                tableRowsPanel.Controls.Add(row);
            }

            tableRowsPanel.ResumeLayout();
        }
    }

    // =========================================================================
    // ADD PRODUCT TO ROOM DIALOG
    // =========================================================================
    public class AddProductToRoomDialog : Form
    {
        private readonly InventoryStore store;
        private readonly RoomData currentRoom;

        private TextBox txtSearch;
        private SmoothFlowPanel existingProductsPanel;
        private TextBox txtNewName;
        private NumericUpDown numNewQty;
        private PictureBox picNewPreview;
        private string newPhotoBase64 = null;

        private bool IsEN => store.Language == "en";
        private string T(string tr, string en) => IsEN ? en : tr;
        private string L(string raw) => AppHelper.LocalizeText(raw, IsEN);

        private Color ColPrimary => AppHelper.GetPrimaryColor(store.ColorTheme, store.DarkMode);
        private Color ColPrimaryFore => AppHelper.GetPrimaryForeColor(store.ColorTheme, store.DarkMode);
        private Color ColSecondary => AppHelper.GetSecondaryColor(store.ColorTheme, store.DarkMode);
        private Color ColSecondaryFore => AppHelper.GetSecondaryForeColor(store.ColorTheme, store.DarkMode);
        private Color ColPrimarySoft => AppHelper.GetPrimarySoftBg(store.ColorTheme, store.DarkMode);
        private Color ColBg => AppHelper.GetBgColor(store.ColorTheme, store.DarkMode);
        private Color ColCard => AppHelper.GetCardColor(store.ColorTheme, store.DarkMode);
        private Color ColTextDark => store.DarkMode ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
        private Color ColTextMuted => store.DarkMode ? Color.FromArgb(161, 161, 170) : Color.FromArgb(82, 82, 91);
        private Color ColBorder => AppHelper.GetBorderColor(store.ColorTheme, store.DarkMode);

        public AddProductToRoomDialog(InventoryStore store, RoomData currentRoom)
        {
            this.store = store;
            this.currentRoom = currentRoom;

            this.Text = T($"Odaya Ürün Ekle — {currentRoom.Name}", $"Add Product to Room — {currentRoom.Name}");
            this.Size = new Size(680, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = ColCard;
            this.ForeColor = ColTextDark;
            this.Font = new Font("Segoe UI", 9.5f);

            BuildDialogUI();
            RenderExistingProductsList();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AppHelper.ApplyWindowTitleBarTheme(this, store.DarkMode);
        }

        private void BuildDialogUI()
        {
            Label lblHeader = new Label
            {
                Text = T("1) Daha Önce Eklenmiş Ürünlerden Seçin", "1) Select from Previously Added Products"),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(18, 14),
                AutoSize = true
            };

            Label lblSearch = new Label
            {
                Text = T("Ürün Ara:", "Search:"),
                ForeColor = ColTextMuted,
                Location = new Point(18, 46),
                AutoSize = true
            };

            txtSearch = new TextBox
            {
                Location = new Point(105, 43),
                Size = new Size(535, 28),
                BackColor = ColBg,
                ForeColor = ColTextDark,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f)
            };
            txtSearch.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                    txtNewName.Text = txtSearch.Text.Trim();
                RenderExistingProductsList();
            };

            existingProductsPanel = new SmoothFlowPanel
            {
                Location = new Point(18, 78),
                Size = new Size(624, 340),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = ColBg,
                BorderStyle = BorderStyle.FixedSingle
            };

            Panel newProdBox = new Panel
            {
                Location = new Point(18, 432),
                Size = new Size(624, 210),
                BackColor = ColPrimarySoft
            };

            Label lblNewTitle = new Label
            {
                Text = T("2) Listede Yok mu? Yeni Ürün Oluştur ve Bu Odaya Ekle", "2) Not in List? Create New Product & Add to Room"),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(14, 10),
                AutoSize = true
            };

            Label lblNewName = new Label { Text = T("Yeni Ürün Adı *", "New Product Name *"), Location = new Point(14, 42), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            txtNewName = new TextBox { Location = new Point(14, 64), Size = new Size(340, 28), BackColor = ColBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f) };

            Label lblNewQty = new Label { Text = T("Başlangıç Adedi", "Initial Qty"), Location = new Point(370, 42), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            numNewQty = new NumericUpDown
            {
                Location = new Point(370, 64),
                Size = new Size(110, 28),
                Minimum = 0,
                Maximum = 100000,
                Value = 1,
                BackColor = ColBg,
                ForeColor = ColTextDark,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            picNewPreview = new PictureBox
            {
                Location = new Point(505, 22),
                Size = new Size(100, 100),
                BackColor = Color.White,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            Button btnPickPhoto = new Button
            {
                Text = T("Ürün Fotoğrafı Seç", "Choose Product Photo"),
                Location = new Point(14, 104),
                Size = new Size(255, 34),
                BackColor = ColCard,
                ForeColor = ColTextDark,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPickPhoto.FlatAppearance.BorderColor = ColBorder;
            btnPickPhoto.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = T("Fotoğraf Seç", "Choose Photo");
                    ofd.Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            newPhotoBase64 = AppHelper.LoadAndResizeImageToBase64(ofd.FileName);
                            byte[] bytes = Convert.FromBase64String(newPhotoBase64);
                            using (var ms = new MemoryStream(bytes))
                            {
                                picNewPreview.Image = new Bitmap(Image.FromStream(ms));
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            };

            Button btnCreateAndAdd = new Button
            {
                Text = T("+ Yeni Ürünü Oluştur ve Odaya Ekle", "+ Create New Product & Add to Room"),
                Location = new Point(14, 152),
                Size = new Size(595, 42),
                BackColor = ColPrimary,
                ForeColor = ColPrimaryFore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCreateAndAdd.FlatAppearance.BorderSize = 0;
            btnCreateAndAdd.Click += (s, e) => CreateNewProductAndAddToRoom();

            newProdBox.Controls.AddRange(new Control[]
            {
                lblNewTitle, lblNewName, txtNewName, lblNewQty, numNewQty,
                picNewPreview, btnPickPhoto, btnCreateAndAdd
            });

            this.Controls.AddRange(new Control[] { lblHeader, lblSearch, txtSearch, existingProductsPanel, newProdBox });
        }

        private void RenderExistingProductsList()
        {
            existingProductsPanel.SuspendLayout();
            foreach (Control c in existingProductsPanel.Controls) c.Dispose();
            existingProductsPanel.Controls.Clear();

            string q = (txtSearch.Text ?? "").Trim().ToLowerInvariant();
            var matched = store.Products
                .Where(p => p.Name.ToLowerInvariant().Contains(q) || L(p.Name).ToLowerInvariant().Contains(q))
                .ToList();

            foreach (var prod in matched)
            {
                bool inThisRoom = currentRoom.HasProduct(prod.Id);
                int qtyInThisRoom = currentRoom.GetQuantity(prod.Id);

                var otherRooms = store.Rooms
                    .Where(r => r.Id != currentRoom.Id && r.HasProduct(prod.Id))
                    .Select(r => $"{r.Name} ({r.GetQuantity(prod.Id)})")
                    .ToList();

                Panel row = new Panel
                {
                    Size = new Size(594, 64),
                    BackColor = ColCard,
                    Margin = new Padding(4, 3, 4, 3)
                };
                row.Paint += (s, e) =>
                {
                    using (Pen p = new Pen(inThisRoom ? ColSecondary : ColBorder, 1))
                        e.Graphics.DrawRectangle(p, 0, 0, row.Width - 1, row.Height - 1);
                };

                PictureBox pic = new PictureBox
                {
                    Location = new Point(8, 7),
                    Size = new Size(50, 50),
                    BackColor = Color.White,
                    SizeMode = PictureBoxSizeMode.Zoom
                };
                Image img = AppHelper.GetProductImage(prod);
                if (img != null) pic.Image = img;

                Label lblName = new Label
                {
                    Text = L(prod.Name),
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(68, 10),
                    Size = new Size(340, 22),
                    AutoEllipsis = true
                };

                string subInfo = otherRooms.Count > 0
                    ? T("Diğer odalar: ", "Other rooms: ") + string.Join(", ", otherRooms)
                    : T("Başka odada yok", "Not in other rooms");
                if (inThisRoom)
                    subInfo = T($"Odada mevcut ({qtyInThisRoom}) • ", $"In room ({qtyInThisRoom}) • ") + subInfo;

                Label lblSub = new Label
                {
                    Text = subInfo,
                    Font = new Font("Segoe UI", 8.2f),
                    ForeColor = inThisRoom ? ColSecondary : ColTextMuted,
                    Location = new Point(68, 34),
                    Size = new Size(350, 18),
                    AutoEllipsis = true
                };

                Button btnPick = new Button
                {
                    Text = inThisRoom ? T("+ 1 Ekle", "+ Add 1") : T("+ Odaya Ekle", "+ Add to Room"),
                    Location = new Point(445, 14),
                    Size = new Size(135, 36),
                    BackColor = inThisRoom ? ColSecondary : ColPrimary,
                    ForeColor = inThisRoom ? ColSecondaryFore : ColPrimaryFore,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnPick.FlatAppearance.BorderSize = 0;
                btnPick.Click += (s, e) =>
                {
                    int current = currentRoom.GetQuantity(prod.Id);
                    currentRoom.SetQuantity(prod.Id, inThisRoom ? current + 1 : 1);
                    AppHelper.SaveStore(store);
                    RenderExistingProductsList();
                };

                row.Controls.AddRange(new Control[] { pic, lblName, lblSub, btnPick });
                existingProductsPanel.Controls.Add(row);
            }

            existingProductsPanel.ResumeLayout();
        }

        private void CreateNewProductAndAddToRoom()
        {
            string name = (txtNewName.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) return;

            int startQty = (int)numNewQty.Value;
            var existing = store.Products.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (!string.IsNullOrEmpty(newPhotoBase64) && string.IsNullOrEmpty(existing.PhotoBase64))
                    existing.PhotoBase64 = newPhotoBase64;
                int current = currentRoom.GetQuantity(existing.Id);
                currentRoom.SetQuantity(existing.Id, current + startQty);
            }
            else
            {
                var newProd = new ProductItem
                {
                    Id = "p_" + DateTime.Now.Ticks,
                    Name = name,
                    PhotoBase64 = newPhotoBase64
                };
                store.Products.Insert(0, newProd);
                currentRoom.SetQuantity(newProd.Id, startQty);
            }

            AppHelper.SaveStore(store);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
