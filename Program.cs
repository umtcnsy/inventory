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
    // VERİ MODELLERİ (XML İLE OTOMATİK KAYIT & FOTOĞRAF DESTEKLİ)
    // =========================================================================
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
        public string ViewMode { get; set; } = "list"; // "list" (Dümdüz Liste) veya "grid" (Kare Kare)
        public bool DarkMode { get; set; } = true;     // Varsayılan Karanlık Mod aktif
        public List<ProductItem> Products { get; set; } = new List<ProductItem>();
        public List<RoomData> Rooms { get; set; } = new List<RoomData>();
    }

    // Çift Tamponlu (Titreme Yapmayan) FlowLayoutPanel
    public class SmoothFlowPanel : FlowLayoutPanel
    {
        public SmoothFlowPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
    }

    // =========================================================================
    // YARDIMCI GÖRSEL, TEMA VE VERİ SINIFI
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
                // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Windows 10 20H1+ & Windows 11)
                DwmSetWindowAttribute(form.Handle, 20, ref useDark, sizeof(int));
                // Fallback attribute 19 for older Windows 10 builds
                DwmSetWindowAttribute(form.Handle, 19, ref useDark, sizeof(int));
            }
            catch { }
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

        public static string LoadAndResizeImageToBase64(string filePath, int maxSize = 420)
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

        public static string CreateSampleProductPhotoBase64(Color bgColor, Color accentColor, string shortCode, string label)
        {
            using (Bitmap bmp = new Bitmap(320, 320))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(bgColor);

                    using (SolidBrush circleBrush = new SolidBrush(Color.FromArgb(40, accentColor)))
                    {
                        g.FillEllipse(circleBrush, 60, 35, 200, 200);
                    }

                    using (Font codeFont = new Font("Segoe UI", 42, FontStyle.Bold))
                    using (SolidBrush accentBrush = new SolidBrush(accentColor))
                    {
                        SizeF codeSize = g.MeasureString(shortCode, codeFont);
                        g.DrawString(shortCode, codeFont, accentBrush, (320 - codeSize.Width) / 2f, 100);
                    }

                    using (Font labelFont = new Font("Segoe UI", 15, FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
                    {
                        SizeF lblSize = g.MeasureString(label, labelFont);
                        g.DrawString(label, labelFont, textBrush, (320 - lblSize.Width) / 2f, 250);
                    }
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static InventoryStore CreateDemoStore(bool darkMode = true)
        {
            var store = new InventoryStore { ViewMode = "list", DarkMode = darkMode };

            var p1 = new ProductItem
            {
                Id = "p_1",
                Name = "Ofis Çalışma Sandalyesi",
                PhotoBase64 = CreateSampleProductPhotoBase64(Color.FromArgb(224, 231, 255), Color.FromArgb(79, 70, 229), "SN", "Çalışma Sandalyesi")
            };
            var p2 = new ProductItem
            {
                Id = "p_2",
                Name = "27\" Dell IPS Monitör",
                PhotoBase64 = CreateSampleProductPhotoBase64(Color.FromArgb(220, 252, 231), Color.FromArgb(22, 163, 74), "MN", "27\" IPS Monitör")
            };
            var p3 = new ProductItem
            {
                Id = "p_3",
                Name = "Kablosuz Klavye & Mouse Seti",
                PhotoBase64 = CreateSampleProductPhotoBase64(Color.FromArgb(254, 243, 199), Color.FromArgb(217, 119, 6), "KM", "Klavye & Mouse")
            };
            var p4 = new ProductItem
            {
                Id = "p_4",
                Name = "HP Lazer Yazıcı",
                PhotoBase64 = CreateSampleProductPhotoBase64(Color.FromArgb(250, 232, 255), Color.FromArgb(147, 51, 234), "YZ", "Lazer Yazıcı")
            };
            var p5 = new ProductItem
            {
                Id = "p_5",
                Name = "Çalışma Masası (160x80)",
                PhotoBase64 = CreateSampleProductPhotoBase64(Color.FromArgb(255, 237, 213), Color.FromArgb(234, 88, 12), "MS", "Çalışma Masası")
            };

            store.Products.AddRange(new[] { p1, p2, p3, p4, p5 });

            var r1 = new RoomData { Id = "r_1", Name = "Yazılım Ofisi (Oda 101)", Note = "1. Kat Sağ Koridor" };
            r1.SetQuantity("p_1", 12);
            r1.SetQuantity("p_2", 14);
            r1.SetQuantity("p_3", 12);
            r1.SetQuantity("p_5", 12);

            var r2 = new RoomData { Id = "r_2", Name = "Ana Depo", Note = "Zemin Kat Arka Bölüm" };
            r2.SetQuantity("p_1", 5);
            r2.SetQuantity("p_2", 3);
            r2.SetQuantity("p_4", 2);

            var r3 = new RoomData { Id = "r_3", Name = "Toplantı Odası", Note = "2. Kat" };
            r3.SetQuantity("p_1", 8);
            r3.SetQuantity("p_2", 1);

            store.Rooms.AddRange(new[] { r1, r2, r3 });
            return store;
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
                MessageBox.Show("Veri kaydedilirken hata oluştu: " + ex.Message, "Kayıt Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        if (loaded != null) return loaded;
                    }
                }
            }
            catch
            {
                // Bozuk dosya durumunda varsayılan yükle
            }
            var demo = CreateDemoStore(true);
            SaveStore(demo);
            return demo;
        }
    }

    // =========================================================================
    // ANA MASAÜSTÜ PENCERESİ (MAIN FORM) - KARANLIK & AÇIK MOD DESTEKLİ
    // =========================================================================
    public class MainForm : Form
    {
        private InventoryStore store;
        private string activeTab = "rooms"; // "rooms", "catalog", "summary"
        private string currentRoomId = null;

        // Dinamik Tema Renkleri (Dark / Light)
        private Color ColBrand => store.DarkMode ? Color.FromArgb(99, 102, 241) : Color.FromArgb(79, 70, 229);
        private Color ColBrandLight => store.DarkMode ? Color.FromArgb(49, 46, 129) : Color.FromArgb(238, 242, 255);
        private Color ColBrandText => store.DarkMode ? Color.FromArgb(199, 210, 254) : Color.FromArgb(79, 70, 229);
        private Color ColBg => store.DarkMode ? Color.FromArgb(15, 23, 42) : Color.FromArgb(241, 245, 249);
        private Color ColCard => store.DarkMode ? Color.FromArgb(30, 41, 59) : Color.White;
        private Color ColSurfaceAlt => store.DarkMode ? Color.FromArgb(51, 65, 85) : Color.FromArgb(241, 245, 249);
        private Color ColInputBg => store.DarkMode ? Color.FromArgb(15, 23, 42) : Color.White;
        private Color ColTextDark => store.DarkMode ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
        private Color ColTextMuted => store.DarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
        private Color ColBorder => store.DarkMode ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
        private Color ColEmerald => store.DarkMode ? Color.FromArgb(52, 211, 153) : Color.FromArgb(16, 185, 129);
        private Color ColDanger => store.DarkMode ? Color.FromArgb(251, 113, 133) : Color.FromArgb(225, 29, 72);
        private Color ColDangerBg => store.DarkMode ? Color.FromArgb(76, 5, 25) : Color.FromArgb(254, 226, 226);

        // UI Kontrolleri
        private Panel topBar;
        private Label lblLogo;
        private Button btnTabRooms;
        private Button btnTabCatalog;
        private Button btnTabSummary;
        private Button btnThemeToggle;
        private Button btnExportCsv;
        private Button btnDemo;
        private Button btnReset;

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
            this.Text = "Envanter & Oda Sayım Uygulaması";
            this.Size = new Size(1260, 790);
            this.MinimumSize = new Size(980, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.DoubleBuffered = true;

            store = AppHelper.LoadStore();
            if (store.Rooms.Count > 0)
                currentRoomId = store.Rooms[0].Id;

            BuildUI();
            ApplyThemeToStaticControls();
            RefreshAll();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AppHelper.ApplyWindowTitleBarTheme(this, store != null && store.DarkMode);
        }

        private void BuildUI()
        {
            // 1. ÜST BAR (TOP HEADER)
            topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                Padding = new Padding(16, 10, 16, 10)
            };
            topBar.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawLine(p, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
            };

            // Sol Başlık
            lblLogo = new Label
            {
                Text = "📦 Envanter & Oda Sayım",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 17)
            };
            topBar.Controls.Add(lblLogo);

            // Orta Sekme Butonları
            btnTabRooms = CreateTabButton("🚪 Odalar ve Sayım", 270);
            btnTabRooms.Click += (s, e) => SwitchTab("rooms");

            btnTabCatalog = CreateTabButton("📦 Ürün Havuzu", 442);
            btnTabCatalog.Width = 152;
            btnTabCatalog.Click += (s, e) => SwitchTab("catalog");

            btnTabSummary = CreateTabButton("📊 Genel Özet", 600);
            btnTabSummary.Width = 145;
            btnTabSummary.Click += (s, e) => SwitchTab("summary");

            topBar.Controls.Add(btnTabRooms);
            topBar.Controls.Add(btnTabCatalog);
            topBar.Controls.Add(btnTabSummary);

            // Sağ Aksiyon Butonları
            FlowLayoutPanel rightActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };

            btnReset = CreateActionButton("Sıfırla", ColDangerBg, ColDanger, 80);
            btnReset.Click += (s, e) => ResetAllData();

            btnDemo = CreateActionButton("Örnek Veri", ColSurfaceAlt, ColTextDark, 95);
            btnDemo.Click += (s, e) => LoadDemoData();

            btnExportCsv = CreateActionButton("📥 Excel İndir", Color.FromArgb(6, 78, 59), Color.FromArgb(110, 231, 183), 125);
            btnExportCsv.Click += (s, e) => ExportToCsv();

            btnThemeToggle = CreateActionButton("☀️ Açık Mod", ColSurfaceAlt, ColTextDark, 125);
            btnThemeToggle.Click += (s, e) => ToggleDarkMode();

            rightActions.Controls.Add(btnReset);
            rightActions.Controls.Add(btnDemo);
            rightActions.Controls.Add(btnExportCsv);
            rightActions.Controls.Add(btnThemeToggle);
            topBar.Controls.Add(rightActions);

            // 2. SOL PANEL (ODALAR LİSTESİ SIDEBAR)
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
                Text = "🔍 Oda Ara:",
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                Location = new Point(4, 74),
                AutoSize = true
            };

            txtRoomSearch = new TextBox
            {
                Location = new Point(74, 71),
                Size = new Size(186, 26),
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

            // 3. ANA İÇERİK ALANI (SAĞ TARAF)
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
                Text = "Oda Adı",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            };

            lblContentStats = new Label
            {
                Text = "0 Çeşit Ürün • 0 Toplam Adet",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(18, 42),
                AutoSize = true
            };

            btnEditRoom = new Button
            {
                Text = "✏️ Odayı Düzenle",
                Size = new Size(125, 30),
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
                Text = "🗑️ Odayı Sil",
                Size = new Size(100, 30),
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
                Text = "🔍 Ürün Ara:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(18, 76),
                AutoSize = true
            };

            txtProductSearch = new TextBox
            {
                Location = new Point(105, 73),
                Size = new Size(270, 28),
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
                Text = "⊞ Kare Kare (Grid)",
                Size = new Size(145, 32),
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

        private void ToggleDarkMode()
        {
            store.DarkMode = !store.DarkMode;
            AppHelper.SaveStore(store);
            AppHelper.ApplyWindowTitleBarTheme(this, store.DarkMode);
            ApplyThemeToStaticControls();
            RefreshAll();
        }

        private void ApplyThemeToStaticControls()
        {
            this.BackColor = ColBg;
            topBar.BackColor = ColCard;
            lblLogo.ForeColor = ColTextDark;

            btnThemeToggle.Text = store.DarkMode ? "☀️ Açık Mod" : "🌙 Karanlık Mod";
            btnThemeToggle.BackColor = store.DarkMode ? Color.FromArgb(51, 65, 85) : Color.FromArgb(30, 41, 59);
            btnThemeToggle.ForeColor = store.DarkMode ? Color.FromArgb(253, 224, 71) : Color.White;

            btnExportCsv.BackColor = store.DarkMode ? Color.FromArgb(6, 78, 59) : Color.FromArgb(209, 250, 229);
            btnExportCsv.ForeColor = store.DarkMode ? Color.FromArgb(110, 231, 183) : Color.FromArgb(6, 95, 70);

            btnDemo.BackColor = ColSurfaceAlt;
            btnDemo.ForeColor = ColTextDark;

            btnReset.BackColor = ColDangerBg;
            btnReset.ForeColor = ColDanger;

            sidebarPanel.BackColor = ColCard;
            sidebarTop.BackColor = ColCard;
            lblRoomsHeader.ForeColor = ColTextMuted;
            btnAddRoom.BackColor = ColBrand;
            btnAddRoom.ForeColor = Color.White;
            lblRoomSearchHint.ForeColor = ColTextMuted;
            txtRoomSearch.BackColor = ColInputBg;
            txtRoomSearch.ForeColor = ColTextDark;
            roomsListPanel.BackColor = ColCard;

            mainArea.BackColor = ColBg;
            contentHeaderPanel.BackColor = ColCard;
            lblContentTitle.ForeColor = ColTextDark;
            lblContentStats.ForeColor = ColBrandText;

            btnEditRoom.BackColor = ColSurfaceAlt;
            btnEditRoom.ForeColor = ColTextDark;

            btnDeleteRoom.BackColor = ColDangerBg;
            btnDeleteRoom.ForeColor = ColDanger;

            btnPrimaryAction.BackColor = ColBrand;
            btnPrimaryAction.ForeColor = Color.White;

            lblProdSearch.ForeColor = ColTextMuted;
            txtProductSearch.BackColor = ColInputBg;
            txtProductSearch.ForeColor = ColTextDark;

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

        private Button CreateTabButton(string text, int x)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, 12),
                Size = new Size(165, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Button CreateActionButton(string text, Color bg, Color fg, int width)
        {
            Button btn = new Button
            {
                Text = text,
                Size = new Size(width, 36),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
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
            btnViewList.BackColor = isList ? ColBrand : ColSurfaceAlt;
            btnViewList.ForeColor = isList ? Color.White : ColTextDark;
            btnViewList.FlatAppearance.BorderColor = isList ? ColBrand : ColBorder;

            btnViewGrid.BackColor = !isList ? ColBrand : ColSurfaceAlt;
            btnViewGrid.ForeColor = !isList ? Color.White : ColTextDark;
            btnViewGrid.FlatAppearance.BorderColor = !isList ? ColBrand : ColBorder;
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
                kv.Value.BackColor = active ? ColBrandLight : ColSurfaceAlt;
                kv.Value.ForeColor = active ? ColBrandText : ColTextMuted;
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
        // SOL PANEL: ODALAR LİSTESİ RENDER
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
                    BackColor = isSelected ? ColBrandLight : (store.DarkMode ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252)),
                    Margin = new Padding(4, 3, 4, 3),
                    Cursor = Cursors.Hand
                };
                card.Paint += (s, e) =>
                {
                    Color borderCol = isSelected ? ColBrand : ColBorder;
                    int bw = isSelected ? 2 : 1;
                    using (Pen p = new Pen(borderCol, bw))
                        e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                };

                Label lblName = new Label
                {
                    Text = room.Name,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = isSelected ? ColBrandText : ColTextDark,
                    Location = new Point(10, 8),
                    Size = new Size(230, 22),
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };

                Label lblNote = new Label
                {
                    Text = string.IsNullOrEmpty(room.Note) ? "Oda Envanteri" : room.Note,
                    Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                    ForeColor = ColTextMuted,
                    Location = new Point(10, 29),
                    Size = new Size(230, 16),
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };

                Label lblCounts = new Label
                {
                    Text = $"{validVariety} Çeşit Ürün  •  Toplam {totalQty} Adet",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = isSelected ? ColBrandText : ColEmerald,
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
        // ANA KARTLAR ALANI (DÜMDÜZ LİSTE VEYA KARE KARE GÖRÜNÜM)
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
                    lblContentTitle.Text = "Henüz Oda Seçilmedi";
                    lblContentStats.Text = "Sol menüden yeni bir oda ekleyin veya seçin.";
                    btnEditRoom.Visible = false;
                    btnDeleteRoom.Visible = false;
                    btnPrimaryAction.Visible = false;
                    mainCardsPanel.ResumeLayout();
                    return;
                }

                btnEditRoom.Visible = true;
                btnDeleteRoom.Visible = true;
                btnPrimaryAction.Visible = true;
                btnPrimaryAction.Text = "+ Odaya Ürün Ekle";

                UpdateCurrentRoomHeaderStats(room);

                var roomProducts = room.Items
                    .Select(entry => new
                    {
                        Product = store.Products.FirstOrDefault(p => p.Id == entry.ProductId),
                        Entry = entry
                    })
                    .Where(x => x.Product != null && x.Product.Name.ToLowerInvariant().Contains(query))
                    .ToList();

                if (roomProducts.Count == 0)
                {
                    mainCardsPanel.Controls.Add(CreateEmptyStatePanel(
                        "Bu Odada Ürün Bulunamadı",
                        "Sağ üstteki '+ Odaya Ürün Ekle' butonuna tıklayarak daha önce başka odalara eklediğiniz ürünlerden seçebilir veya yeni fotoğraflı ürün ekleyebilirsiniz."
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
                btnPrimaryAction.Text = "+ Havuza Ürün Tanımla";

                lblContentTitle.Text = "Genel Ürün Havuzu (Katalog)";
                lblContentStats.Text = $"Toplam {store.Products.Count} Kayıtlı Ürün Çeşidi (Tüm odalarda ortak kullanılır)";

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
                lblContentTitle.Text = "Genel Özet & Oda Dağılım Raporu";
                lblContentStats.Text = $"{store.Rooms.Count} Oda  •  {store.Products.Count} Ürün Çeşidi  •  Genel Toplam: {totalSayim} Adet";

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
            lblContentStats.Text = $"{variety} Çeşit Ürün  •  Toplam {total} Adet Sayıldı";
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
        // GÖRÜNÜM 1: ODA İÇİ "DÜMDÜZ LİSTE" KARTI (+ / - / ELLE SAYI GİRİŞİ)
        // =====================================================================
        private Control CreateRoomProductListCard(RoomData room, ProductItem product, int quantity, int width)
        {
            Panel card = new Panel
            {
                Tag = "list-card",
                Size = new Size(width, 82),
                BackColor = ColCard,
                Margin = new Padding(2, 4, 2, 4)
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            PictureBox pic = CreateProductPictureBox(product, new Size(62, 62), new Point(10, 10));
            card.Controls.Add(pic);

            Label lblName = new Label
            {
                Text = product.Name,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(84, 14),
                Size = new Size(width - 330, 26),
                AutoEllipsis = true
            };

            Button btnEditProd = new Button
            {
                Text = "📷 Fotoğraf / Ad Düzenle",
                Location = new Point(84, 44),
                Size = new Size(155, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColSurfaceAlt,
                ForeColor = ColTextMuted,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnEditProd.FlatAppearance.BorderSize = 0;
            btnEditProd.Click += (s, e) => OpenProductEditDialog(product);

            Button btnRemoveFromRoom = new Button
            {
                Text = "🗑️ Odadan Kaldır",
                Location = new Point(246, 44),
                Size = new Size(118, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColDangerBg,
                ForeColor = ColDanger,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRemoveFromRoom.FlatAppearance.BorderSize = 0;
            btnRemoveFromRoom.Click += (s, e) => RemoveProductFromCurrentRoom(room, product);

            Panel qtyControlBox = CreateQuantityControlBar(room, product, quantity, new Point(width - 196, 20));

            card.Resize += (s, e) =>
            {
                lblName.Width = Math.Max(150, card.Width - 310);
                qtyControlBox.Location = new Point(card.Width - 196, 20);
            };

            card.Controls.Add(lblName);
            card.Controls.Add(btnEditProd);
            card.Controls.Add(btnRemoveFromRoom);
            card.Controls.Add(qtyControlBox);

            return card;
        }

        // =====================================================================
        // GÖRÜNÜM 2: ODA İÇİ "KARE KARE (GRID)" KARTI (+ / - / ELLE SAYI GİRİŞİ)
        // =====================================================================
        private Control CreateRoomProductGridCard(RoomData room, ProductItem product, int quantity)
        {
            Panel card = new Panel
            {
                Size = new Size(218, 276),
                BackColor = ColCard,
                Margin = new Padding(6)
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            PictureBox pic = CreateProductPictureBox(product, new Size(198, 145), new Point(10, 10));
            card.Controls.Add(pic);

            Label lblName = new Label
            {
                Text = product.Name,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(10, 160),
                Size = new Size(198, 38),
                AutoEllipsis = true
            };

            Button btnEdit = new Button
            {
                Text = "📷 Düzenle",
                Location = new Point(10, 198),
                Size = new Size(96, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColSurfaceAlt,
                ForeColor = ColTextMuted,
                Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnEdit.FlatAppearance.BorderSize = 0;
            btnEdit.Click += (s, e) => OpenProductEditDialog(product);

            Button btnDel = new Button
            {
                Text = "🗑️ Kaldır",
                Location = new Point(112, 198),
                Size = new Size(96, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColDangerBg,
                ForeColor = ColDanger,
                Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDel.FlatAppearance.BorderSize = 0;
            btnDel.Click += (s, e) => RemoveProductFromCurrentRoom(room, product);

            Panel qtyBar = CreateQuantityControlBar(room, product, quantity, new Point(19, 228));

            card.Controls.Add(lblName);
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
                BackColor = ColBrand,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPlus.FlatAppearance.BorderSize = 0;

            bool suppressTextEvent = false;

            txtQty.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                {
                    e.Handled = true;
                }
            };

            txtQty.TextChanged += (s, e) =>
            {
                if (suppressTextEvent) return;
                int parsed = 0;
                if (!string.IsNullOrWhiteSpace(txtQty.Text))
                {
                    int.TryParse(txtQty.Text.Trim(), out parsed);
                }
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
                BackColor = ColSurfaceAlt,
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
                pic.Paint += (s, e) =>
                {
                    using (Font f = new Font("Segoe UI", 8f, FontStyle.Bold))
                    using (SolidBrush b = new SolidBrush(ColTextMuted))
                    {
                        StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        e.Graphics.DrawString("📷\nFotoğraf\nEkle", f, b, new RectangleF(0, 0, pic.Width, pic.Height), sf);
                    }
                };
                pic.Click += (s, e) => OpenProductEditDialog(product);
            }

            return pic;
        }

        // =====================================================================
        // ÜRÜN HAVUZU VE GENEL ÖZET KARTLARI (LİSTE & KARE)
        // =====================================================================
        private Control CreateCatalogSummaryCardList(ProductItem prod, int width, bool isCatalogMode)
        {
            Panel card = new Panel
            {
                Tag = "list-card",
                Size = new Size(width, 82),
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

            int totalCount = store.Rooms.Sum(r => r.GetQuantity(prod.Id));
            var roomsBreakdown = store.Rooms
                .Where(r => r.GetQuantity(prod.Id) > 0)
                .Select(r => $"{r.Name}: {r.GetQuantity(prod.Id)}")
                .ToList();

            string breakdownText = roomsBreakdown.Count > 0
                ? "Oda Dağılımı: " + string.Join("  |  ", roomsBreakdown)
                : "Henüz hiçbir odada sayılmadı (0 adet)";

            Label lblName = new Label
            {
                Text = prod.Name,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(84, 14),
                Size = new Size(width - 330, 24),
                AutoEllipsis = true
            };

            Label lblBreakdown = new Label
            {
                Text = breakdownText,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Regular),
                ForeColor = ColTextMuted,
                Location = new Point(84, 42),
                Size = new Size(width - 330, 22),
                AutoEllipsis = true
            };

            Label lblTotalBadge = new Label
            {
                Text = $"Toplam: {totalCount} Adet",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ColEmerald,
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
                    Text = "✏️ Düzenle",
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
                Text = $"Genel Toplam: {totalCount} Adet",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = ColEmerald,
                Location = new Point(10, 202),
                AutoSize = true
            };

            card.Controls.Add(lblName);
            card.Controls.Add(lblTotal);

            if (isCatalogMode)
            {
                Button btnEdit = new Button
                {
                    Text = "📷 Fotoğraf / Ad",
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
                    Text = "🗑️ Sil",
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
                    Text = roomsBreakdown.Count > 0 ? string.Join(", ", roomsBreakdown) : "Odalarda yok",
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
        // DİYALOGLAR VE AKSİYONLAR
        // =====================================================================
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
                dlg.Text = existingRoom == null ? "Yeni Oda / Bölüm Ekle" : "Oda Bilgilerini Düzenle";
                dlg.Size = new Size(420, 265);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = ColCard;
                dlg.ForeColor = ColTextDark;
                dlg.Font = new Font("Segoe UI", 9.5f);
                dlg.HandleCreated += (s, e) => AppHelper.ApplyWindowTitleBarTheme(dlg, store.DarkMode);

                Label lbl1 = new Label { Text = "Oda / Bölüm Adı *", Location = new Point(20, 20), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                TextBox txtName = new TextBox { Location = new Point(20, 44), Width = 360, BackColor = ColInputBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Text = existingRoom != null ? existingRoom.Name : "" };

                Label lbl2 = new Label { Text = "Açıklama / Kat Bilgisi (İsteğe Bağlı)", Location = new Point(20, 86), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                TextBox txtNote = new TextBox { Location = new Point(20, 110), Width = 360, BackColor = ColInputBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Text = existingRoom != null ? existingRoom.Note : "" };

                Button btnSave = new Button
                {
                    Text = "Kaydet",
                    Location = new Point(265, 165),
                    Size = new Size(115, 38),
                    BackColor = ColBrand,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnSave.FlatAppearance.BorderSize = 0;

                btnSave.Click += (s, e) =>
                {
                    string name = txtName.Text.Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        MessageBox.Show("Lütfen oda adını girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

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
                $"\"{room.Name}\" odasını ve içindeki sayım kayıtlarını silmek istediğinize emin misiniz?\n(Ürün havuzundaki ürünler silinmez)",
                "Odayı Sil",
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
                $"\"{product.Name}\" ürününü bu odadan kaldırmak istiyor musunuz?",
                "Odadan Kaldır",
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
                $"\"{product.Name}\" ürününü genel havuzdan ve tüm odalardan tamamen silmek istediğinize emin misiniz?",
                "Ürünü Sil",
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
                dlg.Text = existingProduct == null ? "Havuza Yeni Ürün Tanımla" : "Ürün Adı ve Fotoğrafını Düzenle";
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

                Label lblName = new Label { Text = "Ürün Adı *", Location = new Point(20, 18), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                TextBox txtName = new TextBox { Location = new Point(20, 42), Width = 400, BackColor = ColInputBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Text = existingProduct != null ? existingProduct.Name : "" };

                Label lblPhoto = new Label { Text = "Ürün Fotoğrafı", Location = new Point(20, 84), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

                PictureBox picPreview = new PictureBox
                {
                    Location = new Point(20, 110),
                    Size = new Size(125, 125),
                    BackColor = ColSurfaceAlt,
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
                    Text = "📷 Bilgisayardan Fotoğraf Seç...",
                    Location = new Point(160, 110),
                    Size = new Size(260, 38),
                    BackColor = ColBrandLight,
                    ForeColor = ColBrandText,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnPickPhoto.FlatAppearance.BorderSize = 0;

                Button btnClearPhoto = new Button
                {
                    Text = "Fotoğrafı Kaldır",
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
                        ofd.Title = "Ürün Fotoğrafı Seçin";
                        ofd.Filter = "Görsel Dosyaları|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
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
                                MessageBox.Show("Fotoğraf yüklenemedi: " + ex.Message);
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
                    Text = "Kaydet",
                    Location = new Point(300, 260),
                    Size = new Size(120, 40),
                    BackColor = ColBrand,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnSave.FlatAppearance.BorderSize = 0;

                btnSave.Click += (s, e) =>
                {
                    string name = txtName.Text.Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        MessageBox.Show("Lütfen ürün adını girin.");
                        return;
                    }

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
                dlg.Text = product.Name + " - Fotoğraf Önizleme";
                dlg.Size = new Size(560, 560);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = Color.FromArgb(15, 23, 42);
                dlg.HandleCreated += (s, e) => AppHelper.ApplyWindowTitleBarTheme(dlg, true);

                PictureBox pic = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = img,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(15, 23, 42)
                };
                dlg.Controls.Add(pic);
                dlg.ShowDialog(this);
            }
        }

        private void ExportToCsv()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Excel (CSV) Raporunu Kaydet";
                sfd.Filter = "CSV Dosyası (*.csv)|*.csv";
                sfd.FileName = $"Envanter_Sayim_Raporu_{DateTime.Now:yyyy-MM-dd}.csv";

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Oda Adi;Oda Aciklamasi;Urun Adi;Sayim Adedi");

                    foreach (var room in store.Rooms)
                    {
                        foreach (var item in room.Items)
                        {
                            var prod = store.Products.FirstOrDefault(p => p.Id == item.ProductId);
                            if (prod != null)
                            {
                                sb.AppendLine($"\"{EscapeCsv(room.Name)}\";\"{EscapeCsv(room.Note)}\";\"{EscapeCsv(prod.Name)}\";{item.Quantity}");
                            }
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine("GENEL TOPLAM OZETI;;;");
                    sb.AppendLine("Urun Adi;Bulundugu Oda Sayisi;Genel Toplam Adet;Oda Dagilimi");

                    foreach (var prod in store.Products)
                    {
                        int total = store.Rooms.Sum(r => r.GetQuantity(prod.Id));
                        var breakdown = store.Rooms
                            .Where(r => r.GetQuantity(prod.Id) > 0)
                            .Select(r => $"{r.Name}: {r.GetQuantity(prod.Id)}")
                            .ToList();

                        sb.AppendLine($"\"{EscapeCsv(prod.Name)}\";{breakdown.Count};{total};\"{EscapeCsv(string.Join(" | ", breakdown))}\"");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show("Envanter raporu Excel (CSV) formatında başarıyla kaydedildi!", "Dışa Aktarıldı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private string EscapeCsv(string val)
        {
            return (val ?? "").Replace("\"", "\"\"");
        }

        private void LoadDemoData()
        {
            var res = MessageBox.Show("Örnek oda ve fotoğraflı ürünleri yüklemek istiyor musunuz?", "Örnek Veri", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                bool keepDark = store.DarkMode;
                AppHelper.ClearImageCache();
                store = AppHelper.CreateDemoStore(keepDark);
                currentRoomId = store.Rooms.FirstOrDefault()?.Id;
                AppHelper.SaveStore(store);
                RefreshAll();
            }
        }

        private void ResetAllData()
        {
            var res = MessageBox.Show("TÜM ODALARI VE ÜRÜNLERİ SİLMEK İSTEDİĞİNİZE EMİN MİSİNİZ?\nBu işlem geri alınamaz.", "Tüm Verileri Sıfırla", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
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
    // ODAYA ÜRÜN EKLEME PENCERESİ (KARANLIK MOD UYUMLU)
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

        private Color ColBrand => store.DarkMode ? Color.FromArgb(99, 102, 241) : Color.FromArgb(79, 70, 229);
        private Color ColBrandLight => store.DarkMode ? Color.FromArgb(49, 46, 129) : Color.FromArgb(238, 242, 255);
        private Color ColBrandText => store.DarkMode ? Color.FromArgb(199, 210, 254) : Color.FromArgb(79, 70, 229);
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

            this.Text = $"Odaya Ürün Ekle — {currentRoom.Name}";
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
                Text = "1) Daha Önce Eklenmiş Ürünlerden Seçin (Diğer Odalardan / Havuzdan)",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = ColTextDark,
                Location = new Point(18, 14),
                AutoSize = true
            };

            Label lblSearch = new Label
            {
                Text = "🔍 Kayıtlı Ürünlerde Ara:",
                ForeColor = ColTextMuted,
                Location = new Point(18, 46),
                AutoSize = true
            };

            txtSearch = new TextBox
            {
                Location = new Point(175, 43),
                Size = new Size(465, 28),
                BackColor = ColBg,
                ForeColor = ColTextDark,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f)
            };
            txtSearch.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtNewName.Text = txtSearch.Text.Trim();
                }
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
                BackColor = ColBrandLight
            };
            newProdBox.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColBrand, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, newProdBox.Width - 1, newProdBox.Height - 1);
            };

            Label lblNewTitle = new Label
            {
                Text = "2) Listede Yok mu? Yeni Ürün Oluştur ve Bu Odaya Ekle",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = ColBrandText,
                Location = new Point(14, 10),
                AutoSize = true
            };

            Label lblNewName = new Label { Text = "Yeni Ürün Adı *", Location = new Point(14, 42), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            txtNewName = new TextBox { Location = new Point(14, 64), Size = new Size(340, 28), BackColor = ColBg, ForeColor = ColTextDark, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f) };

            Label lblNewQty = new Label { Text = "Başlangıç Adedi", Location = new Point(370, 42), AutoSize = true, ForeColor = ColTextDark, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
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
                BackColor = ColCard,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            Button btnPickPhoto = new Button
            {
                Text = "📷 Ürün Fotoğrafı Seç (İsteğe Bağlı)",
                Location = new Point(14, 104),
                Size = new Size(255, 34),
                BackColor = ColCard,
                ForeColor = ColBrandText,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPickPhoto.FlatAppearance.BorderColor = ColBrand;
            btnPickPhoto.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Ürün Fotoğrafı Seçin";
                    ofd.Filter = "Görsel Dosyaları|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
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
                            MessageBox.Show("Fotoğraf yüklenemedi: " + ex.Message);
                        }
                    }
                }
            };

            Button btnCreateAndAdd = new Button
            {
                Text = "+ Yeni Ürünü Oluştur ve Odaya Ekle",
                Location = new Point(14, 152),
                Size = new Size(595, 42),
                BackColor = ColBrand,
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

                Color rowBg = inThisRoom
                    ? (store.DarkMode ? Color.FromArgb(6, 78, 59) : Color.FromArgb(236, 253, 245))
                    : ColCard;

                Panel row = new Panel
                {
                    Size = new Size(594, 64),
                    BackColor = rowBg,
                    Margin = new Padding(4, 3, 4, 3)
                };
                row.Paint += (s, e) =>
                {
                    using (Pen p = new Pen(ColBorder, 1))
                        e.Graphics.DrawRectangle(p, 0, 0, row.Width - 1, row.Height - 1);
                };

                PictureBox pic = new PictureBox
                {
                    Location = new Point(8, 7),
                    Size = new Size(50, 50),
                    BackColor = ColSurfaceAlt,
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
                    ? "Diğer odalar: " + string.Join(", ", otherRooms)
                    : "Başka odada henüz yok";
                if (inThisRoom)
                    subInfo = $"Bu odada mevcut ({qtyInThisRoom} adet)  •  " + subInfo;

                Label lblSub = new Label
                {
                    Text = subInfo,
                    Font = new Font("Segoe UI", 8.2f),
                    ForeColor = inThisRoom
                        ? (store.DarkMode ? Color.FromArgb(110, 231, 183) : Color.FromArgb(5, 150, 105))
                        : ColTextMuted,
                    Location = new Point(68, 34),
                    Size = new Size(350, 18),
                    AutoEllipsis = true
                };

                Button btnPick = new Button
                {
                    Text = inThisRoom ? "+ 1 Adet Ekle" : "+ Odaya Ekle",
                    Location = new Point(445, 14),
                    Size = new Size(135, 36),
                    BackColor = inThisRoom ? Color.FromArgb(16, 185, 129) : ColBrand,
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
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Lütfen yeni ürünün adını girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int startQty = (int)numNewQty.Value;

            var existing = store.Products.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (!string.IsNullOrEmpty(newPhotoBase64) && string.IsNullOrEmpty(existing.PhotoBase64))
                {
                    existing.PhotoBase64 = newPhotoBase64;
                }
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

    // =========================================================================
    // PROGRAM GİRİŞ NOKTASI
    // =========================================================================
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
