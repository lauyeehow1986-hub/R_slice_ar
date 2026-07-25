using System;
using System.Collections.Generic;
using UnityEngine;

namespace SliceAR
{
    /// <summary>
    /// Supported UI languages. Order is fixed and must match the column order of every string array in
    /// <see cref="Loc"/> (EN first so it can serve as the fallback). The set covers the original app's six
    /// (English, Italian, Spanish, German, Japanese, French) plus Singapore's three non-English official
    /// languages (Simplified Chinese, Malay, Tamil).
    /// </summary>
    public enum Language { EN, IT, ES, DE, JA, FR, ZH, MS, TA }

    /// <summary>
    /// Tiny runtime localization layer for the code-built UI. There are no scene Text objects to wire, so
    /// rather than the full Unity Localization asset pipeline we keep a compact in-code string table keyed
    /// by a short id; <see cref="T"/> returns the string for the current language (falling back to English).
    ///
    /// The selection is a static so it survives scene switches, and is persisted in PlayerPrefs so it
    /// survives app restarts. UI components subscribe to <see cref="LanguageChanged"/> to re-render their
    /// text (and re-pick a glyph-appropriate font via <see cref="AppFont"/>) when the user changes language.
    ///
    /// Not translated on purpose: the "Slice-AR" brand name, the anatomical orientation letters
    /// (R/L/A/P/S/I — international radiology convention), and the "mm" unit.
    /// </summary>
    public static class Loc
    {
        private const string PrefKey = "slice_ar_language";

        /// <summary>Raised after the active language changes so UIs can refresh their text.</summary>
        public static event Action LanguageChanged;

        public static Language Current { get; private set; }

        /// <summary>All languages in enum order — used to build the cycle button.</summary>
        public static readonly Language[] All =
            { Language.EN, Language.IT, Language.ES, Language.DE, Language.JA,
              Language.FR, Language.ZH, Language.MS, Language.TA };

        // Endonyms (each language's own name for itself) for the language picker button.
        private static readonly string[] Names =
            { "English", "Italiano", "Espanol", "Deutsch", "日本語",
              "Francais", "中文", "Bahasa Melayu", "தமிழ்" };

        static Loc()
        {
            int saved = PlayerPrefs.GetInt(PrefKey, (int)Language.EN);
            Current = (saved >= 0 && saved < All.Length) ? (Language)saved : Language.EN;
        }

        public static string DisplayName(Language lang) => Names[(int)lang];

        public static void SetLanguage(Language lang)
        {
            if (lang == Current)
                return;
            Current = lang;
            PlayerPrefs.SetInt(PrefKey, (int)lang);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke();
        }

        /// <summary>Advance to the next language (wraps), for the cycle button.</summary>
        public static void CycleLanguage()
        {
            SetLanguage(All[(((int)Current) + 1) % All.Length]);
        }

        /// <summary>Localized string for <paramref name="key"/>; English fallback, then the raw key.</summary>
        public static string T(string key)
        {
            if (Table.TryGetValue(key, out string[] row))
            {
                int i = (int)Current;
                if (i < row.Length && !string.IsNullOrEmpty(row[i]))
                    return row[i];
                return row[0]; // English fallback
            }
            return key;
        }

        // Columns are in Language enum order: EN, IT, ES, DE, JA, FR, ZH, MS, TA.
        // This file is UTF-8; CJK/Tamil strings are stored as literal glyphs. Latin languages drop
        // accents (plain ASCII) to stay robust to any encoding hiccup — meaning is unaffected.
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // --- Bottom-centre controls ---------------------------------------------------------------
            ["recenter"] = new[]
            {
                "Recenter", "Ricentra", "Recentrar", "Zentrieren",
                "中央に戻す",                 // JA
                "Recentrer",
                "重新居中",                       // ZH
                "Tengah semula",
                "மையப்படுத்து", // TA
            },
            ["mode"] = new[]
            {
                "Mode", "Modalita", "Modo", "Modus",
                "モード",                             // JA
                "Mode",
                "模式",                                   // ZH
                "Mod",
                "முறை",                       // TA
            },
            ["mode.clip"] = new[]
            {
                "Clip", "Ritaglio", "Recorte", "Schnitt",
                "クリップ",                       // JA
                "Decoupe",
                "剖切",                                   // ZH
                "Kerat",
                "வெட்டு",           // TA
            },
            ["mode.slice"] = new[]
            {
                "Slice", "Sezione", "Corte", "Schicht",
                "スライス",                       // JA
                "Coupe",
                "切片",                                   // ZH
                "Hiris",
                "துண்டு",           // TA
            },
            ["axis"] = new[]
            {
                "Axis", "Asse", "Eje", "Achse",
                "軸",                                         // JA
                "Axe",
                "轴向",                                   // ZH
                "Paksi",
                "அச்சு",                 // TA
            },
            ["axis.axial"] = new[]
            {
                "Axial", "Assiale", "Axial", "Axial",
                "横断",                                   // JA
                "Axial",
                "横断面",                             // ZH
                "Aksial",
                "குறுக்குவெட்டு", // TA
            },
            ["axis.coronal"] = new[]
            {
                "Coronal", "Coronale", "Coronal", "Koronal",
                "冠状",                                   // JA
                "Coronal",
                "冠状面",                             // ZH
                "Koronal",
                "முகப்பு",     // TA
            },
            ["axis.sagittal"] = new[]
            {
                "Sagittal", "Sagittale", "Sagital", "Sagittal",
                "矢状",                                   // JA
                "Sagittal",
                "矢状面",                             // ZH
                "Sagital",
                "பக்கவாட்டு", // TA
            },

            // --- Scene switch -------------------------------------------------------------------------
            ["scene.ar"] = new[]
            {
                "AR mode", "Modalita AR", "Modo AR", "AR-Modus",
                "ARモード",                           // JA
                "Mode AR",
                "AR 模式",                                // ZH
                "Mod AR",
                "AR முறை",                    // TA
            },
            ["scene.3d"] = new[]
            {
                "3D view", "Vista 3D", "Vista 3D", "3D-Ansicht",
                "3D表示",                                 // JA
                "Vue 3D",
                "3D 视图",                                // ZH
                "Paparan 3D",
                "3D காட்சி",        // TA
            },

            // --- Colour LUT palette names -------------------------------------------------------------
            ["lut.grayscale"] = new[]
            {
                "Grayscale", "Scala di grigi", "Escala de grises", "Graustufen",
                "グレースケール",     // JA
                "Niveaux de gris",
                "灰度",                                   // ZH
                "Skala kelabu",
                "சாம்பல்",     // TA
            },
            ["lut.hotmetal"] = new[]
            {
                "Hot Metal", "Metallo caldo", "Metal caliente", "Heissmetall",
                "ホットメタル",           // JA
                "Metal chaud",
                "热金属",                             // ZH
                "Logam panas",
                "சூடான உலோகம்", // TA
            },
            ["lut.rainbow"] = new[]
            {
                "Rainbow", "Arcobaleno", "Arcoiris", "Regenbogen",
                "レインボー",                 // JA
                "Arc-en-ciel",
                "彩虹",                                   // ZH
                "Pelangi",
                "வானவில்",     // TA
            },
            ["lut.cool"] = new[]
            {
                "Cool", "Freddo", "Frio", "Kuhl",
                "クール",                             // JA
                "Froid",
                "冷色",                                   // ZH
                "Sejuk",
                "குளிர்",           // TA
            },

            // --- Annotation controls ------------------------------------------------------------------
            ["annot.marker"] = new[]
            {
                "＋ Marker", "＋ Segno", "＋ Marca", "＋ Marker",
                "＋ マーカー",                // JA
                "＋ Repere",
                "＋ 标记",                            // ZH
                "＋ Penanda",
                "＋ குறி",                // TA
            },
            ["annot.measure"] = new[]
            {
                "Measure", "Misura", "Medir", "Messen",
                "計測",                                   // JA
                "Mesurer",
                "测量",                                   // ZH
                "Ukur",
                "அளவிடு",           // TA
            },
            ["annot.delete"] = new[]
            {
                "Delete", "Elimina", "Eliminar", "Loschen",
                "削除",                                   // JA
                "Supprimer",
                "删除",                                   // ZH
                "Padam",
                "நீக்கு",           // TA
            },
            ["annot.segs"] = new[]
            {
                "segs", "segm.", "segm.", "Segm.",
                "区間",                                   // JA
                "segm.",
                "段",                                         // ZH
                "segmen",
                "பிரிவு",           // TA
            },

            // --- Disclaimer ---------------------------------------------------------------------------
            ["disclaimer.ack"] = new[]
            {
                "I Understand", "Ho capito", "Entendido", "Verstanden",
                "了解しました",           // JA
                "J'ai compris",
                "我明白了",                       // ZH
                "Saya faham",
                "புரிந்தது", // TA
            },
            ["disclaimer.footer"] = new[]
            {
                "For education & research only - not for diagnostic use",
                "Solo per didattica e ricerca - non per uso diagnostico",
                "Solo para educacion e investigacion - no para uso diagnostico",
                "Nur fur Lehre und Forschung - nicht fur die Diagnostik",
                "教育・研究用途のみ - 診断には使用できません", // JA
                "Uniquement pour l'enseignement et la recherche - pas pour le diagnostic",
                "仅用于教育与研究 - 不可用于诊断", // ZH
                "Untuk pendidikan & penyelidikan sahaja - bukan untuk diagnosis",
                "கல்வி மற்றும் ஆராய்ச்சிக்கு மட்டுமே - நோயறிதல்ல", // TA
            },
            ["disclaimer.body"] = new[]
            {
                // EN
                "Slice-AR is an educational and research tool for exploring 3D medical volume data.\n\n" +
                "It is NOT a medical device and must NOT be used for diagnosis, treatment, or any clinical " +
                "decision-making.\n\n" +
                "Bundled datasets are de-identified or synthetic. Only load data you are authorised to use - " +
                "never real patient studies without consent.",
                // IT
                "Slice-AR e uno strumento didattico e di ricerca per esplorare dati volumetrici medici 3D.\n\n" +
                "NON e un dispositivo medico e NON deve essere usato per diagnosi, trattamento o qualsiasi " +
                "decisione clinica.\n\n" +
                "I dataset inclusi sono anonimizzati o sintetici. Carica solo dati che sei autorizzato a usare - " +
                "mai studi reali di pazienti senza consenso.",
                // ES
                "Slice-AR es una herramienta educativa y de investigacion para explorar datos volumetricos " +
                "medicos en 3D.\n\n" +
                "NO es un dispositivo medico y NO debe usarse para diagnostico, tratamiento ni ninguna " +
                "decision clinica.\n\n" +
                "Los conjuntos de datos incluidos estan anonimizados o son sinteticos. Carga solo datos que " +
                "estes autorizado a usar - nunca estudios reales de pacientes sin consentimiento.",
                // DE
                "Slice-AR ist ein Werkzeug fur Lehre und Forschung zur Erkundung medizinischer " +
                "3D-Volumendaten.\n\n" +
                "Es ist KEIN Medizinprodukt und darf NICHT fur Diagnose, Behandlung oder klinische " +
                "Entscheidungen verwendet werden.\n\n" +
                "Mitgelieferte Datensatze sind anonymisiert oder synthetisch. Laden Sie nur Daten, zu deren " +
                "Nutzung Sie berechtigt sind - niemals echte Patientenstudien ohne Einwilligung.",
                // JA
                "Slice-AR は3D医用ボリュームデータを" +
                "閲覧するための教育・研究用" +
                "ツールです。\n\n" +
                "本アプリは医療機器ではありません。" +
                "診断、治療、臨床上の判断には" +
                "使用しないでください。\n\n" +
                "付属のデータセットは匿名化" +
                "または合成されたものです。" +
                "使用が許可されたデータのみを" +
                "読み込み、同意のない実際の" +
                "患者データは決して使用しない" +
                "でください。",
                // FR
                "Slice-AR est un outil pedagogique et de recherche pour explorer des donnees volumiques " +
                "medicales en 3D.\n\n" +
                "Ce N'est PAS un dispositif medical et ne doit PAS etre utilise pour le diagnostic, le " +
                "traitement ou toute decision clinique.\n\n" +
                "Les jeux de donnees fournis sont anonymises ou synthetiques. Ne chargez que des donnees que " +
                "vous etes autorise a utiliser - jamais de veritables etudes de patients sans consentement.",
                // ZH
                "Slice-AR 是一款用于浏览三维医学体" +
                "数据的教育与研究工具。\n\n" +
                "它不是医疗器械，不得用于诊断、" +
                "治疗或任何临床决策。\n\n" +
                "随附的数据集均已去标识化或为" +
                "合成数据。请仅加载您有权使用" +
                "的数据 - 切勿在未经同意的情况下" +
                "使用真实患者数据。",
                // MS
                "Slice-AR ialah alat pendidikan dan penyelidikan untuk meneroka data volum perubatan 3D.\n\n" +
                "Ia BUKAN peranti perubatan dan TIDAK boleh digunakan untuk diagnosis, rawatan atau sebarang " +
                "keputusan klinikal.\n\n" +
                "Set data yang disertakan telah dinyahkenal pasti atau sintetik. Muatkan hanya data yang anda " +
                "dibenarkan menggunakan - jangan sekali-kali kajian pesakit sebenar tanpa kebenaran.",
                // TA
                "Slice-AR என்பது 3D மருத்துவ " +
                "தொகுதித் தரவை " +
                "ஆராய்வதற்கான " +
                "கல்வி மற்றும் " +
                "ஆராய்ச்சி கருவி" +
                "யாகும்.\n\n" +
                "இது ஒரு மருத்துவ " +
                "சாதனம் அல்ல; " +
                "நோயறிதல், சிகிச்சை " +
                "அல்லது எந்த மருத்துவ " +
                "முடிவெடுப்பிற்கும் " +
                "பயன்படுத்தக் கூடாது.\n\n" +
                "இணைக்கப்பட்ட தரவுத்" +
                "தொகுப்புகள் அடையாளம் " +
                "நீக்கப்பட்டவை அல்லது " +
                "செயற்கையானவை. நீங்கள் " +
                "பயன்படுத்த அனுமதிக்கப்" +
                "பட்ட தரவை மட்டுமே " +
                "ஏற்றவும் - சம்மதமின்றி " +
                "உண்மையான நோயாளர் தரவை " +
                "ஒருபோதும் பயன்படுத்த " +
                "வேண்டாம்.",
            },

            // --- Import panel (device-storage dataset import) -----------------------------------------
            // File-type tokens (.zip, PNG/JPG, RAW, DICOM, mm) are kept literal across all languages.
            ["import.open"] = new[]
            {
                "Import", "Importa", "Importar", "Importieren",
                "インポート", "Importer", "导入", "Import", "இறக்கு",
            },
            ["import.title"] = new[]
            {
                "Import dataset", "Importa dataset", "Importar conjunto", "Datensatz importieren",
                "データセットをインポート", "Importer un jeu de donnees",
                "导入数据集", "Import set data", "தரவுத்தொகுப்பை இறக்கு",
            },
            ["import.voxel"] = new[]
            {
                "Voxel size (mm)", "Dimensione voxel (mm)", "Tamano de voxel (mm)", "Voxelgrosse (mm)",
                "ボクセルサイズ (mm)", "Taille du voxel (mm)",
                "体素大小 (mm)", "Saiz voxel (mm)", "வோக்செல் அளவு (mm)",
            },
            ["import.tf"] = new[]
            {
                "Transfer func", "Funz. trasfer.", "Func. transf.", "Transferfunk.",
                "伝達関数", "Fonction transf.", "传递函数", "Fungsi pindah", "இடமாற்று சார்பு",
            },
            ["import.seq_hint"] = new[]
            {
                "Image sequence — .zip of PNG/JPG slices",
                "Sequenza di immagini — .zip di slice PNG/JPG",
                "Secuencia de imagenes — .zip de cortes PNG/JPG",
                "Bildsequenz — .zip aus PNG/JPG-Schichten",
                "画像シーケンス — PNG/JPGスライスの.zip",
                "Sequence d'images — .zip de coupes PNG/JPG",
                "图像序列 — PNG/JPG 切片的 .zip",
                "Jujukan imej — .zip hirisan PNG/JPG",
                "பட வரிசை — PNG/JPG துண்டுகளின் .zip",
            },
            ["import.pick_seq"] = new[]
            {
                "Pick image stack (.zip)…", "Scegli stack immagini (.zip)…", "Elegir pila de imagenes (.zip)…",
                "Bildstapel wahlen (.zip)…", "画像スタックを選択 (.zip)…", "Choisir la pile d'images (.zip)…",
                "选择图像堆栈 (.zip)…", "Pilih tindanan imej (.zip)…", "பட அடுக்கைத் தேர்ந்தெடு (.zip)…",
            },
            ["import.or_raw"] = new[]
            {
                "— or headerless RAW —", "— oppure RAW senza header —", "— o RAW sin encabezado —",
                "— oder RAW ohne Header —", "— またはヘッダーなしRAW —", "— ou RAW sans en-tete —",
                "— 或无头 RAW —", "— atau RAW tanpa pengepala —", "— அல்லது தலைப்பில்லா RAW —",
            },
            ["import.dims"] = new[]
            {
                "Dimensions", "Dimensioni", "Dimensiones", "Abmessungen",
                "寸法", "Dimensions", "尺寸", "Dimensi", "பரிமாணங்கள்",
            },
            ["import.dtype"] = new[]
            {
                "Data type", "Tipo di dato", "Tipo de dato", "Datentyp",
                "データ型", "Type de donnees", "数据类型", "Jenis data", "தரவு வகை",
            },
            ["import.pick_raw"] = new[]
            {
                "Pick RAW file…", "Scegli file RAW…", "Elegir archivo RAW…", "RAW-Datei wahlen…",
                "RAWファイルを選択…", "Choisir un fichier RAW…", "选择 RAW 文件…", "Pilih fail RAW…",
                "RAW கோப்பைத் தேர்ந்தெடு…",
            },
            ["import.or_dicom"] = new[]
            {
                "— or DICOM (.zip, uncompressed) —", "— oppure DICOM (.zip, non compresso) —",
                "— o DICOM (.zip, sin comprimir) —", "— oder DICOM (.zip, unkomprimiert) —",
                "— またはDICOM (.zip、非圧縮) —", "— ou DICOM (.zip, non compresse) —",
                "— 或 DICOM (.zip，未压缩) —", "— atau DICOM (.zip, tak dimampat) —",
                "— அல்லது DICOM (.zip, சுருக்கப்படாத) —",
            },
            ["import.pick_dicom"] = new[]
            {
                "Pick DICOM (.zip)…", "Scegli DICOM (.zip)…", "Elegir DICOM (.zip)…", "DICOM wahlen (.zip)…",
                "DICOMを選択 (.zip)…", "Choisir DICOM (.zip)…", "选择 DICOM (.zip)…", "Pilih DICOM (.zip)…",
                "DICOM (.zip) தேர்ந்தெடு…",
            },
            ["import.cancel"] = new[]
            {
                "Cancel", "Annulla", "Cancelar", "Abbrechen",
                "キャンセル", "Annuler", "取消", "Batal", "ரத்து",
            },
            ["import.opening"] = new[]
            {
                "Opening picker…", "Apertura selettore…", "Abriendo selector…", "Auswahl wird geoffnet…",
                "ピッカーを開いています…", "Ouverture du selecteur…", "正在打开选择器…", "Membuka pemilih…",
                "தேர்வியைத் திறக்கிறது…",
            },
            ["import.cancelled"] = new[]
            {
                "Cancelled.", "Annullato.", "Cancelado.", "Abgebrochen.",
                "キャンセルされました。", "Annule.", "已取消。", "Dibatalkan.", "ரத்து செய்யப்பட்டது.",
            },
            ["import.loading"] = new[]
            {
                "Loading…", "Caricamento…", "Cargando…", "Wird geladen…",
                "読み込み中…", "Chargement…", "加载中…", "Memuatkan…", "ஏற்றுகிறது…",
            },
        };
    }
}
