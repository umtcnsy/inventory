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
    // VERİ MODELLERİ (SERİ NO TABLOSU: NO | SERİ NO | DURUM DESTEKLİ)
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
        public bool DarkMode { get; set; } = true;     // Koyu / Açık Mod
        public string Language { get; set; } = "tr";   // "tr" | "en"
        public int ColorTheme { get; set; } = 0;       // 0: Okyanus&Turkuaz, 1: Zümrüt&Altın, 2: Kızıl&Turuncu, 3: Mor&Pembe
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
    // YARDIMCI GÖRSEL, DİL, TEMA VE VERİ SINIFI
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

        // Sadece adı "laptop" içeren ürünlerde Seri No tablosu gösterilsin
        public static bool IsLaptopProduct(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName)) return false;
            return productName.IndexOf("laptop", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // 4 İkili Renk Paleti (Primary + Secondary)
        public static Color GetPrimaryColor(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(16, 185, 129) : Color.FromArgb(5, 150, 105);   // Zümrüt Yeşili
                case 2: return dark ? Color.FromArgb(244, 63, 94) : Color.FromArgb(225, 29, 72);    // Kızıl Rose
                case 3: return dark ? Color.FromArgb(139, 92, 246) : Color.FromArgb(124, 58, 237);  // Kraliyet Moru
                default: return dark ? Color.FromArgb(14, 165, 233) : Color.FromArgb(2, 132, 199);  // Okyanus Mavisi (Varsayılan)
            }
        }

        public static Color GetSecondaryColor(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(245, 158, 11) : Color.FromArgb(217, 119, 6);   // Altın Kehribar
                case 2: return dark ? Color.FromArgb(249, 115, 22) : Color.FromArgb(234, 88, 12);   // Gün Batımı Turuncu
                case 3: return dark ? Color.FromArgb(236, 72, 153) : Color.FromArgb(219, 39, 119);  // Fuşya Pembe
                default: return dark ? Color.FromArgb(20, 184, 166) : Color.FromArgb(13, 148, 136); // Turkuaz (Varsayılan)
            }
        }

        public static Color GetPrimarySoftBg(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(6, 78, 59) : Color.FromArgb(209, 250, 229);
                case 2: return dark ? Color.FromArgb(136, 19, 55) : Color.FromArgb(255, 228, 230);
                case 3: return dark ? Color.FromArgb(76, 29, 149) : Color.FromArgb(237, 233, 254);
                default: return dark ? Color.FromArgb(12, 74, 110) : Color.FromArgb(224, 242, 254);
            }
        }

        public static Color GetSecondarySoftBg(int themeIdx, bool dark)
        {
            switch (themeIdx)
            {
                case 1: return dark ? Color.FromArgb(120, 53, 15) : Color.FromArgb(254, 243, 199);
                case 2: return dark ? Color.FromArgb(124, 45, 18) : Color.FromArgb(255, 237, 213);
                case 3: return dark ? Color.FromArgb(131, 24, 67) : Color.FromArgb(252, 231, 243);
                default: return dark ? Color.FromArgb(19, 78, 74) : Color.FromArgb(204, 251, 241);
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
                MessageBox.Show("Save Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
    // ANA MASAÜSTÜ PENCERESİ (SOL ÜST AYARLAR + 4 İKİLİ RENK + TR/EN + SERİ NO TABLOSU)
    // =========================================================================
    public class MainForm : Form
    {
        private InventoryStore store;
        private string activeTab = "rooms"; // "rooms", "catalog", "summary"
        private string currentRoomId = null;

        private bool IsEN => store.Language == "en";
        private string T(string tr, string en) => IsEN ? en : tr;

        // Dinamik İkili Renk & Tema Paleti
        private Color ColPrimary => AppHelper.GetPrimaryColor(store.ColorTheme, store.DarkMode);
        private Color ColSecondary => AppHelper.GetSecondaryColor(store.ColorTheme, store.DarkMode);
        private Color ColPrimarySoft => AppHelper.GetPrimarySoftBg(store.ColorTheme, store.DarkMode);
        private Color ColSecondarySoft => AppHelper.GetSecondarySoftBg(store.ColorTheme, store.DarkMode);

        private Color ColBg => store.DarkMode ? Color.FromArgb(15, 23, 42) : Color.FromArgb(241, 245, 249);
        private Color ColCard => store.DarkMode ? Color.FromArgb(30, 41, 59) : Color.White;
        private Color ColSurfaceAlt => store.DarkMode ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
        private Color ColInputBg => store.DarkMode ? Color.FromArgb(15, 23, 42) : Color.White;
        private Color ColTextDark => store.DarkMode ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
        private Color ColTextMuted => store.DarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
        private Color ColBorder => store.DarkMode ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
        private Color ColDanger => store.DarkMode ? Color.FromArgb(251, 113, 133) : Color.FromArgb(225, 29, 72);
        private Color ColDangerBg => store.DarkMode ? Color.FromArgb(76, 5, 25) : Color.FromArgb(254, 226, 226);

        // UI Kontrolleri
        private Panel topBar;
        private Button btnTopLeftSettings; // SOL ÜST AYARLAR BUTONU
        private Label lblLogo;
        private Button btnTabRooms;
        private Button btnTabCatalog;
        private Button btnTabSummary;
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
            this.MinimumSize = new Size(980, 620);
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
            // 1. ÜST BAR (SOL ÜSTTE AYARLAR BUTONU İLE)
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

            // SOL ÜST AYARLAR BUTONU
            btnTopLeftSettings = new Button
            {
                Text = "⚙️ Ayarlar",
                Location = new Point(14, 12),
                Size = new Size(120, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTopLeftSettings.FlatAppearance.BorderSize = 1;
            btnTopLeftSettings.Click += (s, e) => OpenTopLeftSettingsDialog();
            topBar.Controls.Add(btnTopLeftSettings);

            // Başlık (Sol Üst Ayarlar butonunun hemen sağında)
            lblLogo = new Label
            {
                Text = "📦 Envanter Sayım",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(146, 18)
            };
            topBar.Controls.Add(lblLogo);

            // Orta Sekme Butonları
            btnTabRooms = CreateTabButton("🚪 Odalar", 350, 155);
            btnTabRooms.Click += (s, e) => SwitchTab("rooms");

            btnTabCatalog = CreateTabButton("📦 Ürün Havuzu", 512, 160);
            btnTabCatalog.Click += (s, e) => SwitchTab("catalog");

            btnTabSummary = CreateTabButton("📊 Genel Özet", 679, 155);
            btnTabSummary.Click += (s, e) => SwitchTab("summary");

            topBar.Controls.Add(btnTabRooms);
            topBar.Controls.Add(btnTabCatalog);
            topBar.Controls.Add(btnTabSummary);

            // Sağ Aksiyon: Excel İndir
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
                Text = "📥 Excel (CSV)",
                Size = new Size(140, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnExportCsv.FlatAppearance.BorderSize = 0;
            btnExportCsv.Click += (s, e) => ExportToCsv();

            rightActions.Controls.Add(btnExportCsv);
            topBar.Controls.Add(rightActions);

            // 2. SOL PANEL (ODALAR SIDEBAR)
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
                Text = "ODALAR / BÖLGELER",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(4, 6),
                AutoSize = true
            };

            btnAddRoom = new Button
            {
                Text = "+ Yeni Oda Ekle",
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
                Text = "🔍 Ara:",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                Location = new Point(4, 74),
                AutoSize = true
            };

            txtRoomSearch = new TextBox
            {
                Location = new Point(62, 71),
                Size = new Size(198, 26),
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

            // 3. ANA İÇERİK ALANI
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
                Text = "inventory",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            };

            lblContentStats = new Label
            {
                Text = "0 Ürün",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(18, 42),
                AutoSize = true
            };

            btnEditRoom = new Button
            {
                Text = "✏️ Düzenle",
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
                Text = "🗑️ Sil",
                Size = new Size(90, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDeleteRoom.FlatAppearance.BorderSize = 0;
            btnDeleteRoom.Click += (s, e) => DeleteCurrentRoom();

            btnPrimaryAction = new Button
            {
                Text = "+ Odaya Ürün Ekle",
                Size = new Size(195, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPrimaryAction.FlatAppearance.BorderSize = 0;
            btnPrimaryAction.Click += (s, e) => OnPrimaryActionClicked();

            lblProdSearch = new Label
            {
                Text = "🔍 Ürün / Seri No Ara:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(18, 76),
                AutoSize = true
            };

            txtProductSearch = new TextBox
            {
                Location = new Point(160, 73),
                Size = new Size(260, 28),
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtProductSearch.TextChanged += (s, e) => RenderMainCards();

            btnViewList = new Button
            {
                Text = "☰ Dümdüz Liste",
                Size = new Size(135, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnViewList.FlatAppearance.BorderSize = 1;
            btnViewList.Click += (s, e) => SetViewMode("list");

            btnViewGrid = new Button
            {
                Text = "⊞ Kare Kare",
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
        // SOL ÜST AYARLAR PENCERESİ (DİL + KOYU/AÇIK MOD + 4 İKİLİ RENK OPSİYONU)
        // =====================================================================
        private void OpenTopLeftSettingsDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = T("⚙️ Uygulama Ayarları", "⚙️ Application Settings");
                dlg.Size = new Size(460, 520);
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

                // 1. Dil Seçeneği (TR / EN)
                Label lblLang = new Label
                {
                    Text = T("🌐 Arayüz Dili / Interface Language", "🌐 Interface Language / Arayüz Dili"),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(20, 16),
                    AutoSize = true
                };

                Button btnTR = new Button
                {
                    Text = "🇹🇷 Türkçe",
                    Location = new Point(20, 42),
                    Size = new Size(195, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = !IsEN ? ColPrimary : ColSurfaceAlt,
                    ForeColor = !IsEN ? Color.White : ColTextDark,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnTR.FlatAppearance.BorderSize = 0;

                Button btnEN = new Button
                {
                    Text = "🇬🇧 English",
                    Location = new Point(225, 42),
                    Size = new Size(195, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = IsEN ? ColPrimary : ColSurfaceAlt,
                    ForeColor = IsEN ? Color.White : ColTextDark,
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

                // 2. Açık / Koyu Mod
                Label lblMode = new Label
                {
                    Text = T("🌓 Görünüm Modu (Açık / Koyu)", "🌓 Appearance Mode (Dark / Light)"),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(20, 96),
                    AutoSize = true
                };

                Button btnDark = new Button
                {
                    Text = T("🌙 Koyu Mod (Dark)", "🌙 Dark Mode"),
                    Location = new Point(20, 122),
                    Size = new Size(195, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = store.DarkMode ? ColPrimary : ColSurfaceAlt,
                    ForeColor = store.DarkMode ? Color.White : ColTextDark,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnDark.FlatAppearance.BorderSize = 0;

                Button btnLight = new Button
                {
                    Text = T("☀️ Açık Mod (Light)", "☀️ Light Mode"),
                    Location = new Point(225, 122),
                    Size = new Size(195, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = !store.DarkMode ? ColPrimary : ColSurfaceAlt,
                    ForeColor = !store.DarkMode ? Color.White : ColTextDark,
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

                // 3. 4 İKİLİ RENK OPSİYONU
                Label lblColors = new Label
                {
                    Text = T("🎨 4 İkili Renk Teması (Dual-Color Themes)", "🎨 4 Dual-Color Theme Options"),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = ColTextDark,
                    Location = new Point(20, 178),
                    AutoSize = true
                };

                string[] themeNames = new[]
                {
                    T("1) 🔵🟢 Okyanus Mavisi & Turkuaz", "1) 🔵🟢 Ocean Blue & Teal"),
                    T("2) 🟢🟡 Zümrüt Yeşili & Altın Kehribar", "2) 🟢🟡 Emerald Green & Amber Gold"),
                    T("3) 🔴🟠 Kızıl Rose & Gün Batımı Turuncu", "3) 🔴🟠 Crimson Rose & Sunset Orange"),
                    T("4) 🟣🩷 Kraliyet Moru & Fuşya Pembe", "4) 🟣🩷 Royal Purple & Fuchsia Pink")
                };

                dlg.Controls.AddRange(new Control[] { lblLang, btnTR, btnEN, lblMode, btnDark, btnLight, lblColors });

                for (int i = 0; i < 4; i++)
                {
                    int idx = i;
                    bool isCurrent = store.ColorTheme == idx;
                    Color pCol = AppHelper.GetPrimaryColor(idx, store.DarkMode);
                    Color sCol = AppHelper.GetSecondaryColor(idx, store.DarkMode);

                    Button btnTheme = new Button
                    {
                        Text = (isCurrent ? "✓  " : "    ") + themeNames[idx],
                        Location = new Point(20, 206 + (idx * 44)),
                        Size = new Size(400, 38),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(12, 0, 0, 0),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = isCurrent ? pCol : ColSurfaceAlt,
                        ForeColor = isCurrent ? Color.White : ColTextDark,
                        Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
                        Cursor = Cursors.Hand
                    };
                    btnTheme.FlatAppearance.BorderColor = isCurrent ? sCol : ColBorder;
                    btnTheme.FlatAppearance.BorderSize = isCurrent ? 2 : 1;

                    // Sağ tarafa ikili renk önizleme kutucukları çiz
                    btnTheme.Paint += (sender, pe) =>
                    {
                        using (SolidBrush b1 = new SolidBrush(pCol))
                        using (SolidBrush b2 = new SolidBrush(sCol))
                        using (Pen borderPen = new Pen(Color.White, 1.5f))
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

                // 4. Alt Veri Sıfırlama / Geri Yükleme Butonları
                Button btnRestoreInventory = new Button
                {
                    Text = T("🔄 Inventory Odasını Geri Yükle", "🔄 Restore Default Inventory"),
                    Location = new Point(20, 400),
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
                    Text = T("🗑️ Tüm Verileri Sıfırla", "🗑️ Reset All Data"),
                    Location = new Point(245, 400),
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
            btnTopLeftSettings.Text = T("⚙️ Ayarlar", "⚙️ Settings");
            btnTopLeftSettings.BackColor = ColPrimarySoft;
            btnTopLeftSettings.ForeColor = store.DarkMode ? Color.White : ColPrimary;
            btnTopLeftSettings.FlatAppearance.BorderColor = ColPrimary;

            lblLogo.Text = T("📦 Envanter Sayım", "📦 Inventory Count");
            lblLogo.ForeColor = ColTextDark;

            btnTabRooms.Text = T("🚪 Odalar ve Sayım", "🚪 Rooms & Count");
            btnTabCatalog.Text = T("📦 Ürün Havuzu", "📦 Product Pool");
            btnTabSummary.Text = T("📊 Genel Özet", "📊 Global Summary");

            btnExportCsv.Text = T("📥 Excel İndir", "📥 Export Excel");
            btnExportCsv.BackColor = ColSecondary;
            btnExportCsv.ForeColor = Color.White;

            sidebarPanel.BackColor = ColCard;
            sidebarTop.BackColor = ColCard;
            lblRoomsHeader.Text = T("ODALAR / BÖLGELER", "ROOMS / LOCATIONS");
            lblRoomsHeader.ForeColor = ColTextMuted;

            btnAddRoom.Text = T("+ Yeni Oda Ekle", "+ Add New Room");
            btnAddRoom.BackColor = ColPrimary;
            btnAddRoom.ForeColor = Color.White;

            lblRoomSearchHint.Text = T("🔍 Ara:", "🔍 Find:");
            lblRoomSearchHint.ForeColor = ColTextMuted;
            txtRoomSearch.BackColor = ColInputBg;
            txtRoomSearch.ForeColor = ColTextDark;
            roomsListPanel.BackColor = ColCard;

            mainArea.BackColor = ColBg;
            contentHeaderPanel.BackColor = ColCard;
            lblContentTitle.ForeColor = ColTextDark;
            lblContentStats.ForeColor = ColSecondary;

            btnEditRoom.Text = T("✏️ Odayı Düzenle", "✏️ Edit Room");
            btnEditRoom.BackColor = ColSurfaceAlt;
            btnEditRoom.ForeColor = ColTextDark;

            btnDeleteRoom.Text = T("🗑️ Odayı Sil", "🗑️ Delete");
            btnDeleteRoom.BackColor = ColDangerBg;
            btnDeleteRoom.ForeColor = ColDanger;

            btnPrimaryAction.BackColor = ColPrimary;
            btnPrimaryAction.ForeColor = Color.White;

            lblProdSearch.Text = T("🔍 Ürün / Seri No:", "🔍 Search Item / SN:");
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
            btnViewList.ForeColor = isList ? Color.White : ColTextDark;
            btnViewList.FlatAppearance.BorderColor = isList ? ColPrimary : ColBorder;

            btnViewGrid.BackColor = !isList ? ColPrimary : ColSurfaceAlt;
            btnViewGrid.ForeColor = !isList ? Color.White : ColTextDark;
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
                kv.Value.ForeColor = active ? Color.White : ColTextMuted;
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
        // SOL PANEL: ODALAR LİSTESİ
        // =====================================================================
        private void RenderRoomsSidebar()
        {
            roomsListPanel.SuspendLayout();
            foreach (Control c in roomsListPanel.Controls) c.Dispose();
            roomsListPanel.Controls.Clear();

            string q = (txtRoomSearch.Text ?? "").Trim().ToLowerInvariant();
            var filtered = store.Rooms.Where(r =>
                r.Name.ToLowerInvariant().Contains(q) ||
                (!string.IsNullOrEmpty(r.Note) && r.Note.ToLowerInvariant().Contains(q))
            ).ToList();

            foreach (var room in filtered)
            {
                bool isSelected = (room.Id == currentRoomId);
                int validVariety = room.Items.Count(i => store.Products.Any(p => p.Id == i.ProductId));
                int totalQty = room.Items.Where(i => store.Products.Any(p => p.Id == i.ProductId)).Sum(i => i.Quantity);

                Panel card = new Panel
                {
                    Size = new Size(252, 72),
                    BackColor = isSelected ? ColPrimarySoft : (store.DarkMode ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252)),
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
                    Text = string.IsNullOrEmpty(room.Note) ? T("Oda Envanteri", "Room Inventory") : room.Note,
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
        // ANA KARTLAR ALANI
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
                        (x.Entry.Serials != null && x.Entry.Serials.Any(sr =>
                            (sr.SerialNo ?? "").ToLowerInvariant().Contains(query) ||
                            (sr.Status ?? "").ToLowerInvariant().Contains(query)))
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
                    .Where(p => p.Name.ToLowerInvariant().Contains(query))
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
                    .Where(p => p.Name.ToLowerInvariant().Contains(query))
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
            lblContentTitle.Text = room.Name + (string.IsNullOrEmpty(room.Note) ? "" : $" ({room.Note})");
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
        // GÖRÜNÜM 1: ODA İÇİ "DÜMDÜZ LİSTE" KARTI (SADECE LAPTOPLARDA SERİ NO TABLOSU)
        // =====================================================================
        private Control CreateRoomProductListCard(RoomData room, ProductItem product, int quantity, int width)
        {
            bool isLaptop = AppHelper.IsLaptopProduct(product.Name);
            var serials = room.GetSerials(product.Id);
            int serialCount = serials.Count;

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
                Text = product.Name,
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
                string preview = string.Join("  |  ", serials.Take(4).Select((sr, i) => $"#{i + 1} {sr.SerialNo} ({sr.Status})"));
                Label lblSerialPreview = new Label
                {
                    Text = "📋 " + preview,
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

            // SADECE ADI LAPTOP OLANLARDA SERİ NO TABLOSU BUTONU GÖRÜNÜR
            if (isLaptop)
            {
                Button btnSerialTable = new Button
                {
                    Text = serialCount > 0
                        ? T($"📋 Seri No Tablosu ({serialCount})", $"📋 Serial No Table ({serialCount})")
                        : T("📋 Seri No Tablosu", "📋 Serial No Table"),
                    Location = new Point(nextBtnX, btnY),
                    Size = new Size(165, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = serialCount > 0 ? ColSecondary : ColPrimarySoft,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                if (serialCount == 0 && !store.DarkMode)
                    btnSerialTable.ForeColor = ColPrimary;
                btnSerialTable.FlatAppearance.BorderSize = 0;
                btnSerialTable.Click += (s, e) => OpenLaptopSerialTableDialog(room, product);
                card.Controls.Add(btnSerialTable);
                nextBtnX += 172;
            }

            Button btnEditProd = new Button
            {
                Text = T("📷 Düzenle", "📷 Edit"),
                Location = new Point(nextBtnX, btnY),
                Size = new Size(92, 28),
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
                Text = T("🗑️ Kaldır", "🗑️ Remove"),
                Location = new Point(nextBtnX + 98, btnY),
                Size = new Size(86, 28),
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
        // GÖRÜNÜM 2: ODA İÇİ "KARE KARE (GRID)" KARTI (SADECE LAPTOPLARDA SERİ NO)
        // =====================================================================
        private Control CreateRoomProductGridCard(RoomData room, ProductItem product, int quantity)
        {
            bool isLaptop = AppHelper.IsLaptopProduct(product.Name);
            var serials = room.GetSerials(product.Id);
            int serialCount = serials.Count;

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
                Text = product.Name,
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
                        ? T($"📋 Seri No Tablosu ({serialCount})", $"📋 Serial No Table ({serialCount})")
                        : T("📋 Seri No Tablosu", "📋 Serial No Table"),
                    Location = new Point(10, 182),
                    Size = new Size(200, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = serialCount > 0 ? ColSecondary : ColPrimarySoft,
                    ForeColor = (serialCount == 0 && !store.DarkMode) ? ColPrimary : Color.White,
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
                Text = T("📷 Düzenle", "📷 Edit"),
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
                Text = T("🗑️ Kaldır", "🗑️ Remove"),
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
        // ORTAK ADET KONTROL BARI: [ - ]  [ ELLE SAYI GİRİŞİ ]  [ + ]
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
                ForeColor = Color.White,
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
                        e.Graphics.DrawString(T("📷\nFotoğraf\nEkle", "📷\nAdd\nPhoto"), f, b, new RectangleF(0, 0, pic.Width, pic.Height), sf);
                    }
                };
                pic.Click += (s, e) => OpenProductEditDialog(product);
            }

            return pic;
        }

        // =====================================================================
        // ÜRÜN HAVUZU VE GENEL ÖZET KARTLARI
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
                    .Select(r => $"{r.Name} ({string.Join(", ", r.GetSerials(prod.Id).Select(sr => sr.SerialNo + ":" + sr.Status))})")
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
                Text = prod.Name,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(84, 12),
                Size = new Size(width - 330, 24),
                AutoEllipsis = true
            };

            Label lblBreakdown = new Label
            {
                Text = breakdownText,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Regular),
                ForeColor = ColTextMuted,
                Location = new Point(84, 38),
                Size = new Size(width - 330, 20),
                AutoEllipsis = true
            };

            if (serialsInRooms.Count > 0)
            {
                Label lblNotes = new Label
                {
                    Text = "📋 " + string.Join("  |  ", serialsInRooms),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = ColSecondary,
                    Location = new Point(84, 62),
                    Size = new Size(width - 310, 20),
                    AutoEllipsis = true
                };
                card.Controls.Add(lblNotes);
            }

            Label lblTotalBadge = new Label
            {
                Text = IsEN ? $"Total: {totalCount}" : $"Toplam: {totalCount} Adet",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ColSecondary,
                Location = new Point(width - 295, 28),
                Size = new Size(145, 26),
                TextAlign = ContentAlignment.MiddleRight
            };

            card.Controls.Add(lblName);
            card.Controls.Add(lblBreakdown);
            card.Controls.Add(lblTotalBadge);

            if (isCatalogMode)
            {
                Button btnEdit = new Button
                {
                    Text = T("✏️ Düzenle", "✏️ Edit"),
                    Location = new Point(width - 140, 24),
                    Size = new Size(85, 32),
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
                    Text = "🗑️",
                    Location = new Point(width - 48, 24),
                    Size = new Size(36, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColDangerBg,
                    ForeColor = ColDanger,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnDel.FlatAppearance.BorderSize = 0;
                btnDel.Click += (s, e) => DeleteProductCompletely(prod);

                card.Controls.Add(btnEdit);
                card.Controls.Add(btnDel);
            }
            else
            {
                lblTotalBadge.Location = new Point(width - 175, 28);
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
                Text = prod.Name,
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
                    Text = T("📷 Düzenle", "📷 Edit"),
                    Location = new Point(10, 228),
                    Size = new Size(140, 28),
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
                    Text = T("🗑️ Sil", "🗑️ Del"),
                    Location = new Point(156, 228),
                    Size = new Size(52, 28),
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

        // =====================================================================
        // 📋 LAPTOP SERİ NO TABLOSU PENCERESİ (NO | SERİ NO | DURUM)
        // =====================================================================
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
                TextBox txtNote = new TextBox { Location = new Point(20, 110), Width = 360, BackColor = ColInputBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Text = existingRoom != null ? existingRoom.Note : "" };

                Button btnSave = new Button
                {
                    Text = T("Kaydet", "Save"),
                    Location = new Point(265, 165),
                    Size = new Size(115, 38),
                    BackColor = ColPrimary,
                    ForeColor = Color.White,
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
                T($"\"{product.Name}\" ürününü bu odadan kaldırmak istiyor musunuz?", $"Remove \"{product.Name}\" from this room?"),
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
                T($"\"{product.Name}\" ürününü tüm odalardan silmek istediğinize emin misiniz?", $"Delete \"{product.Name}\" from catalog and all rooms?"),
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
                TextBox txtName = new TextBox { Location = new Point(20, 42), Width = 400, BackColor = ColInputBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Text = existingProduct != null ? existingProduct.Name : "" };

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
                    Text = T("📷 Bilgisayardan Fotoğraf Seç...", "📷 Choose Photo from PC..."),
                    Location = new Point(160, 110),
                    Size = new Size(260, 38),
                    BackColor = ColPrimarySoft,
                    ForeColor = store.DarkMode ? Color.White : ColPrimary,
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
                    ForeColor = Color.White,
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
                dlg.Text = product.Name;
                dlg.Size = new Size(560, 560);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = Color.FromArgb(15, 23, 42);
                dlg.HandleCreated += (s, e) => AppHelper.ApplyWindowTitleBarTheme(dlg, true);

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

        private void ExportToCsv()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = T("Excel (CSV) Raporunu Kaydet", "Save Excel (CSV) Report");
                sfd.Filter = "CSV (*.csv)|*.csv";
                sfd.FileName = $"Inventory_Report_{DateTime.Now:yyyy-MM-dd}.csv";

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(T("Oda Adi;Aciklama;Urun Adi;Adet;Seri No Tablosu (NO - SERI NO - DURUM)", "Room;Note;Product;Quantity;Serial Table (NO - SERIAL NO - STATUS)"));

                    foreach (var room in store.Rooms)
                    {
                        foreach (var item in room.Items)
                        {
                            var prod = store.Products.FirstOrDefault(p => p.Id == item.ProductId);
                            if (prod != null)
                            {
                                var serials = item.Serials ?? new List<SerialRecord>();
                                string serialsText = string.Join(" | ", serials.Select((sr, idx) => $"#{idx + 1} {sr.SerialNo} [{sr.Status}]"));
                                sb.AppendLine($"\"{EscapeCsv(room.Name)}\";\"{EscapeCsv(room.Note)}\";\"{EscapeCsv(prod.Name)}\";{item.Quantity};\"{EscapeCsv(serialsText)}\"");
                            }
                        }
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show(T("Excel (CSV) raporu kaydedildi!", "Excel (CSV) report saved!"), "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
    // 📋 LAPTOP SERİ NO TABLOSU PENCERESİ (PAINT ÖRNEĞİNDEKİ GİBİ: NO | SERİ NO | DURUM)
    // =========================================================================
    public class LaptopSerialTableDialog : Form
    {
        private readonly InventoryStore store;
        private readonly RoomData room;
        private readonly ProductItem product;
        private readonly List<SerialRecord> serials;

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

        private Color ColPrimary => AppHelper.GetPrimaryColor(store.ColorTheme, store.DarkMode);
        private Color ColSecondary => AppHelper.GetSecondaryColor(store.ColorTheme, store.DarkMode);
        private Color ColBg => store.DarkMode ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
        private Color ColCard => store.DarkMode ? Color.FromArgb(30, 41, 59) : Color.White;
        private Color ColSurfaceAlt => store.DarkMode ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
        private Color ColTextDark => store.DarkMode ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
        private Color ColTextMuted => store.DarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
        private Color ColBorder => store.DarkMode ? Color.FromArgb(100, 116, 139) : Color.FromArgb(30, 41, 59);
        private Color ColDanger => store.DarkMode ? Color.FromArgb(251, 113, 133) : Color.FromArgb(225, 29, 72);
        private Color ColDangerBg => store.DarkMode ? Color.FromArgb(76, 5, 25) : Color.FromArgb(254, 226, 226);

        public LaptopSerialTableDialog(InventoryStore store, RoomData room, ProductItem product)
        {
            this.store = store;
            this.room = room;
            this.product = product;
            this.serials = room.GetSerials(product.Id);

            this.Text = T($"📋 Laptop Seri No Tablosu — {product.Name}", $"📋 Laptop Serial Number Table — {product.Name}");
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
            // Üst Laptop Bilgi Kartı
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
                Text = $"💻 {product.Name.ToUpperInvariant()} — " + T("SERİ NO & DURUM TABLOSU", "SERIAL NO & STATUS TABLE"),
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(74, 10),
                AutoSize = true
            };

            lblSubInfo = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColSecondary,
                Location = new Point(74, 36),
                AutoSize = true
            };

            topInfo.Controls.AddRange(new Control[] { pic, lblTitle, lblSubInfo });

            // Seri No ve Durum Ekleme / Düzeltme Formu
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
                ForeColor = Color.White,
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

            // =================================================================
            // PAINT ÇİZİMİNDEKİ TABLO BAŞLIĞI: | NO | SERİ NO | DURUM | İŞLEM |
            // =================================================================
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

            // Tablo Gövdesi (Satırlar)
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

            // Alt Butonlar
            btnSyncQty = new Button
            {
                Text = "",
                Location = new Point(18, 550),
                Size = new Size(340, 40),
                BackColor = ColSecondary,
                ForeColor = Color.White,
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
                    T($"\"{product.Name}\" adedi tablodaki seri no sayısına ({serials.Count}) eşitlendi.",
                      $"\"{product.Name}\" quantity synced to {serials.Count}."),
                    "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnClose = new Button
            {
                Text = T("💾 Kaydet ve Kapat", "💾 Save & Close"),
                Location = new Point(526, 550),
                Size = new Size(160, 40),
                BackColor = ColPrimary,
                ForeColor = Color.White,
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
            txtStatus.Text = rec.Status;
            btnAddOrUpdate.Text = T("💾 Güncelle", "💾 Update");
            btnAddOrUpdate.BackColor = ColSecondary;
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

        private void UpdateCounters()
        {
            lblSubInfo.Text = IsEN
                ? $"Room: {room.Name}   •   Room Quantity: {room.GetQuantity(product.Id)}   •   Recorded Serials: {serials.Count}"
                : $"Oda: {room.Name}   •   Oda Sayım Adedi: {room.GetQuantity(product.Id)}   •   Tablodaki Seri No: {serials.Count}";
            btnSyncQty.Text = IsEN
                ? $"🔄 Sync Item Quantity to Serial Count ({serials.Count})"
                : $"🔄 Ürün Adedini Seri No Sayısına Eşitle ({serials.Count})";
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
                    Text = rec.Status,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                    ForeColor = ColTextDark,
                    Location = new Point(334, 10),
                    Size = new Size(192, 22),
                    AutoEllipsis = true
                };

                // ✏️ DÜZELT Butonu
                Button btnEdit = new Button
                {
                    Text = T("✏️ Düzelt", "✏️ Edit"),
                    Location = new Point(542, 7),
                    Size = new Size(62, 28),
                    BackColor = ColSurfaceAlt,
                    ForeColor = ColTextDark,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnEdit.FlatAppearance.BorderSize = 0;
                btnEdit.Click += (s, e) => StartEditRow(rec);

                // 🗑️ SİL Butonu
                Button btnDel = new Button
                {
                    Text = "🗑️",
                    Location = new Point(608, 7),
                    Size = new Size(30, 28),
                    BackColor = ColDangerBg,
                    ForeColor = ColDanger,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
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
    // ODAYA ÜRÜN EKLEME PENCERESİ
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

        private Color ColPrimary => AppHelper.GetPrimaryColor(store.ColorTheme, store.DarkMode);
        private Color ColSecondary => AppHelper.GetSecondaryColor(store.ColorTheme, store.DarkMode);
        private Color ColPrimarySoft => AppHelper.GetPrimarySoftBg(store.ColorTheme, store.DarkMode);
        private Color ColBg => store.DarkMode ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
        private Color ColCard => store.DarkMode ? Color.FromArgb(30, 41, 59) : Color.White;
        private Color ColSurfaceAlt => store.DarkMode ? Color.FromArgb(51, 65, 85) : Color.FromArgb(241, 245, 249);
        private Color ColTextDark => store.DarkMode ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
        private Color ColTextMuted => store.DarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
        private Color ColBorder => store.DarkMode ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);

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
                Text = T("🔍 Ürün Ara:", "🔍 Search:"),
                ForeColor = ColTextMuted,
                Location = new Point(18, 46),
                AutoSize = true
            };

            txtSearch = new TextBox
            {
                Location = new Point(135, 43),
                Size = new Size(505, 28),
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
                Text = T("📷 Ürün Fotoğrafı Seç", "📷 Choose Photo"),
                Location = new Point(14, 104),
                Size = new Size(255, 34),
                BackColor = ColCard,
                ForeColor = ColTextDark,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPickPhoto.FlatAppearance.BorderColor = ColPrimary;
            btnPickPhoto.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
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
                ForeColor = Color.White,
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
                .Where(p => p.Name.ToLowerInvariant().Contains(q))
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
                    Text = prod.Name,
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
                    ForeColor = Color.White,
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
