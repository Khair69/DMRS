/* DMRS graduation defense deck — Arabic RTL, medical teal design */
const pptxgen = require("pptxgenjs");

const DOCS = "D:/Code/ASP/DMRS/docs";
const OUT = DOCS + "/عرض المشروع.pptx";

const pptx = new pptxgen();
pptx.layout = "LAYOUT_WIDE"; // 13.33 x 7.5
pptx.rtlMode = true;
pptx.theme = { headFontFace: "Arial", bodyFontFace: "Arial" };

const W = 13.333, H = 7.5, MX = 0.6;
const CW = W - 2 * MX; // 12.13

// palette — medical teal + amber accent
const C = {
  deep: "0A3540",   // dark bg
  deep2: "10485A",  // card on dark
  teal: "0E7C86",   // primary
  mint: "02C39A",   // accent
  amber: "E9B44C",  // sharp accent (numbers on dark)
  ink: "12333C",    // body text
  muted: "587078",  // secondary text
  card: "F2F8F8",   // light card bg
  line: "D9E7E9",   // borders
  ice: "CFE8EA",    // light text on dark
  white: "FFFFFF",
};

const FONT = "Arial";

// ---- helpers (always return FRESH objects; pptxgenjs mutates options) ----
const ar = (o) => Object.assign({ fontFace: FONT, lang: "ar-SA", rtlMode: true, align: "right", color: C.ink }, o);
const arC = (o) => Object.assign({ fontFace: FONT, lang: "ar-SA", rtlMode: true, align: "center", color: C.ink }, o);
const en = (o) => Object.assign({ fontFace: FONT, color: C.ink }, o);

// PowerPoint scrambles Latin+digit phrases inside a single Arabic RTL run
// ("HL7 FHIR R5" → "5FHIR R7 HL"). The reliable encoding — the same one
// PowerPoint itself produces when you type mixed text — is separate runs:
// Latin islands as lang="en-US" runs, Arabic segments as lang="ar-SA" runs,
// with RLM anchors so boundary spaces don't collapse.
const RLM = "‏";
const ISLAND_RE = /[A-Za-z0-9][A-Za-z0-9 .\/\-+&':()\[\]]*[A-Za-z0-9%)\]]|[A-Za-z0-9]/g;
function splitIslands(str) {
  const parts = [];
  let last = 0, m;
  ISLAND_RE.lastIndex = 0;
  while ((m = ISLAND_RE.exec(str))) {
    let s = m.index, e = ISLAND_RE.lastIndex, seg = m[0];
    let before = str.slice(last, s);
    if (before) {
      if (/\s$/.test(before)) before += RLM; // anchor trailing space
      parts.push({ ar: true, t: before });
    }
    parts.push({ ar: false, t: seg });
    last = e;
  }
  const rest = str.slice(last);
  if (rest) parts.push({ ar: true, t: rest });
  for (let i = 1; i < parts.length; i++) {
    if (parts[i].ar && !parts[i - 1].ar) parts[i].t = RLM + parts[i].t; // anchor leading space/punct
  }
  return parts;
}
function splitRun(text, runOpts) {
  const parts = splitIslands(text);
  return parts.map((p, i) => {
    const o = Object.assign({}, runOpts || {});
    if (i > 0) delete o.bullet;                 // bullet only on first subrun
    if (i < parts.length - 1) delete o.breakLine; // breakLine only on last subrun
    if (!p.ar) o.lang = "en-US";
    o.rtlMode = true; // paragraph-level rtl must reach the pPr of every paragraph
    return { text: p.t, options: o };
  });
}
function fixBidiTxt(txt, opts) {
  if (!opts || !opts.rtlMode) return [txt, opts];
  if (typeof txt === "string") return [splitRun(txt, {}), opts];
  if (Array.isArray(txt)) {
    const out = [];
    for (const r of txt) {
      if (r && typeof r === "object" && typeof r.text === "string") out.push(...splitRun(r.text, r.options));
      else out.push(r);
    }
    return [out, opts];
  }
  return [txt, opts];
}

let pageNo = 0;
function newSlide({ dark = false, footer = true } = {}) {
  const s = pptx.addSlide();
  const origAddText = s.addText.bind(s);
  s.addText = (txt, opts) => { const [t2, o2] = fixBidiTxt(txt, opts); return origAddText(t2, o2); };
  pageNo++;
  s.background = { color: dark ? C.deep : C.white };
  if (footer) {
    s.addText("نظام السجلات الطبية الرقمية — DMRS", ar({
      x: W - MX - 4.6, y: 7.08, w: 4.6, h: 0.3, fontSize: 9,
      color: dark ? C.ice : C.muted, align: "right", margin: 0,
    }));
    s.addText(String(pageNo), en({
      x: MX, y: 7.08, w: 0.6, h: 0.3, fontSize: 9,
      color: dark ? C.ice : C.muted, align: "left", margin: 0,
    }));
  }
  return s;
}

function header(s, title, subtitle) {
  s.addText(title, ar({ x: MX, y: 0.32, w: CW, h: 0.62, fontSize: 29, bold: true, color: C.deep, margin: 0 }));
  if (subtitle) {
    s.addText(subtitle, ar({ x: MX, y: 0.94, w: CW, h: 0.38, fontSize: 14, color: C.teal, margin: 0 }));
  }
}

function circleNum(s, txt, cx, cy, d, fill, color) {
  s.addShape(pptx.ShapeType.ellipse, { x: cx, y: cy, w: d, h: d, fill: { color: fill || C.teal } });
  s.addText(txt, en({ x: cx, y: cy, w: d, h: d, align: "center", valign: "middle", fontSize: d >= 0.55 ? 18 : 14, bold: true, color: color || C.white, margin: 0 }));
}

function card(s, x, y, w, h, opts = {}) {
  s.addShape(pptx.ShapeType.roundRect, {
    x, y, w, h, rectRadius: 0.08,
    fill: { color: opts.fill || C.card },
    line: { color: opts.line || C.line, width: 0.75 },
  });
}

function framedImage(s, path, x, y, w, h) {
  s.addShape(pptx.ShapeType.rect, {
    x: x - 0.04, y: y - 0.04, w: w + 0.08, h: h + 0.08,
    fill: { color: C.white }, line: { color: C.line, width: 1 },
    shadow: { type: "outer", color: "808f93", blur: 6, offset: 2, angle: 90, opacity: 0.35 },
  });
  s.addImage({ path, x, y, w, h });
}

function deco(s) { // soft circles on dark slides
  s.addShape(pptx.ShapeType.ellipse, { x: -1.6, y: 4.9, w: 5.2, h: 5.2, fill: { color: C.white, transparency: 95 }, line: { type: "none" } });
  s.addShape(pptx.ShapeType.ellipse, { x: 10.6, y: -2.4, w: 4.6, h: 4.6, fill: { color: C.mint, transparency: 88 }, line: { type: "none" } });
  s.addShape(pptx.ShapeType.ellipse, { x: 11.9, y: 5.9, w: 2.4, h: 2.4, fill: { color: C.amber, transparency: 88 }, line: { type: "none" } });
}

// =========================================================================
// 1 — TITLE
// =========================================================================
{
  const s = newSlide({ dark: true, footer: false });
  deco(s);
  s.addText("الجمهورية العربية السورية — وزارة التعليم العالي والبحث العلمي", arC({ x: MX, y: 0.5, w: CW, h: 0.32, fontSize: 13, color: C.ice }));
  s.addText("الجامعة الوطنية الخاصة — كلية الهندسة — قسم هندسة الحاسوب", arC({ x: MX, y: 0.84, w: CW, h: 0.32, fontSize: 13, color: C.ice }));

  s.addShape(pptx.ShapeType.roundRect, { x: W / 2 - 1.05, y: 1.5, w: 2.1, h: 0.46, rectRadius: 0.23, fill: { color: C.amber } });
  s.addText("مشروع تخرّج", arC({ x: W / 2 - 1.05, y: 1.5, w: 2.1, h: 0.46, fontSize: 15, bold: true, color: C.deep, valign: "middle", margin: 0 }));

  s.addText("نظام السجلات الطبية الرقمية", arC({ x: MX, y: 2.2, w: CW, h: 0.95, fontSize: 44, bold: true, color: C.white }));
  s.addText("Digital Medical Records System — DMRS", en({ x: MX, y: 3.18, w: CW, h: 0.5, fontSize: 20, bold: true, color: C.mint, align: "center" }));
  s.addText("منصّة صحّية قائمة على معيار HL7 FHIR R5 مع دعم القرار السريري والذكاء الاصطناعي التنبؤي", arC({ x: MX, y: 3.75, w: CW, h: 0.45, fontSize: 15, italic: true, color: C.ice }));

  s.addText([
    { text: "إعداد الطلاب", options: { bold: true, color: C.mint, breakLine: true, fontSize: 14 } },
    { text: "أمير المنصور      محمد خير الحوراني      ميزر عمري", options: { color: C.white, fontSize: 17, bold: true } },
  ], arC({ x: MX, y: 4.7, w: CW, h: 0.95, lineSpacingMultiple: 1.4 }));

  s.addText([
    { text: "بإشراف", options: { bold: true, color: C.mint, breakLine: true, fontSize: 14 } },
    { text: "د. وسيم رمضان", options: { color: C.white, fontSize: 16, bold: true } },
  ], arC({ x: MX, y: 5.75, w: CW, h: 0.85, lineSpacingMultiple: 1.4 }));

  s.addText("العام الدراسي ٢٠٢٥ – ٢٠٢٦", arC({ x: MX, y: 6.75, w: CW, h: 0.35, fontSize: 13, color: C.ice }));

  s.addNotes("السلام عليكم. نرحّب بلجنة التحكيم الموقّرة، ونعرض مشروع تخرّجنا: نظام السجلات الطبية الرقمية DMRS — منصّة صحّية قائمة على معيار HL7 FHIR مع دعم القرار السريري والذكاء الاصطناعي التنبؤي.");
}

// =========================================================================
// 2 — OUTLINE
// =========================================================================
{
  const s = newSlide();
  header(s, "مخطّط العرض", "محاور تقديم المشروع خلال عشر دقائق");
  const items = [
    "المقدمة والتحديات",
    "الأهداف وأهمّ الميزات",
    "التقنيات والبنية العامة",
    "طبقات النظام: FHIR · دعم القرار · الذكاء الاصطناعي",
    "الدراسة التحليلية والمخطّطات",
    "التطبيق العملي (الواجهات)",
    "الآفاق المستقبلية والخاتمة",
  ];
  // two columns RTL: first column on the right
  const colW = 5.85, gap = 0.43, x0 = W - MX - colW, x1 = MX;
  items.forEach((t, i) => {
    const col = i < 4 ? 0 : 1;
    const row = col === 0 ? i : i - 4;
    const x = col === 0 ? x0 : x1;
    const y = 1.75 + row * 1.28;
    card(s, x, y, colW, 1.02);
    circleNum(s, String(i + 1), x + colW - 0.95, y + 0.23, 0.56, C.teal);
    s.addText(t, ar({ x: x + 0.3, y: y, w: colW - 1.45, h: 1.02, fontSize: 16, bold: true, valign: "middle", margin: 0 }));
  });
  s.addNotes("سنمرّ في هذا العرض على سبعة محاور: المقدمة والتحديات، فالأهداف والميزات، ثم التقنيات والبنية العامة، ثم الطبقات الذكية للنظام، فالدراسة التحليلية، ثم لمحة عن الواجهات — التي سيتناولها العرض العملي المسجّل بالتفصيل — وأخيراً الآفاق المستقبلية والخاتمة.");
}

// =========================================================================
// 3 — INTRO
// =========================================================================
{
  const s = newSlide();
  header(s, "المقدمة", "لماذا نظام السجلات الطبية الرقمية؟");
  s.addText(
    "تُعدّ السجلات الطبية حجر الأساس في تقديم رعاية صحّية آمنة وفعّالة، إلا أنّها غالباً ما تكون موزّعة بين أنظمةٍ غير متوافقة لا يتبادل بعضها البيانات مع بعض، ما يضعف التنسيق بين مقدّمي الرعاية، ويؤدّي إلى تكرار الفحوص، وإلى قراراتٍ سريرية تُتّخذ من دون صورةٍ كاملة عن حالة المريض. كما تبقى الرعاية في كثيرٍ من الأنظمة تفاعليةً لا استباقية.",
    ar({ x: MX, y: 1.65, w: CW, h: 1.5, fontSize: 15.5, lineSpacingMultiple: 1.35, valign: "top" })
  );
  const pillars = [
    ["سجلّ موحّد", "تمثيل البيانات وفق المعيار العالمي HL7 FHIR R5 يضمن قابلية التشغيل البيني بين الأنظمة الصحية."],
    ["دعم ذكي للقرار", "تنبيهات سريرية في موضع الرعاية عبر محرّك قواعد مرن قابل للتهيئة وفق مواصفة CDS Hooks."],
    ["ذكاء تنبّؤي", "نماذج تُقدّر مخاطر المريض من بياناته الفعلية، فتنقل الرعاية من الطابع التفاعلي إلى الاستباقي."],
  ];
  const pw = 3.9, pg = 0.22;
  pillars.forEach(([t, d], i) => {
    const x = W - MX - pw - i * (pw + pg); // RTL order
    card(s, x, 3.55, pw, 2.95, { fill: C.card });
    s.addShape(pptx.ShapeType.ellipse, { x: x + pw / 2 - 0.33, y: 3.9, w: 0.66, h: 0.66, fill: { color: [C.teal, C.mint, C.amber][i] } });
    s.addText(["١", "٢", "٣"][i], arC({ x: x + pw / 2 - 0.33, y: 3.9, w: 0.66, h: 0.66, fontSize: 20, bold: true, color: i === 2 ? C.deep : C.white, valign: "middle", margin: 0 }));
    s.addText(t, arC({ x: x + 0.25, y: 4.72, w: pw - 0.5, h: 0.42, fontSize: 17, bold: true, color: C.deep }));
    s.addText(d, arC({ x: x + 0.3, y: 5.18, w: pw - 0.6, h: 1.2, fontSize: 12.5, color: C.muted, lineSpacingMultiple: 1.25, valign: "top" }));
  });
  s.addNotes("السجلات الطبية هي ذاكرة المريض، لكنّها في الواقع مبعثرة بين أنظمة مغلقة لا تتخاطب. من هنا جاءت فكرة المشروع: منصّة تجمع ثلاث ركائز — سجلّ موحّد وفق معيار عالمي، ودعم ذكي للقرار السريري، وذكاء اصطناعي تنبّؤي.");
}

// =========================================================================
// 4 — CHALLENGES
// =========================================================================
{
  const s = newSlide();
  header(s, "التحديات التي يعالجها المشروع", "خمسة تحدّيات واقعية في المنظومة الصحية");
  const items = [
    ["تشتّت البيانات وانعدام التشغيل البيني", "تُخزَّن بيانات المريض في أنظمةٍ متفرّقة وبصِيَغٍ مغلقة، فلا ينتقل سجلّه معه بين مقدّمي الرعاية، وتتكرّر الفحوص."],
    ["ضعف الدعم الاستباقي للقرار", "غياب أدواتٍ تنبّه الطبيب آنياً إلى المخاطر — كجرعةٍ زائدة أو تداخلٍ دوائي — يُبقي الأخطاء الطبية احتمالاً قائماً."],
    ["صعوبة تتبّع تاريخ السجل", "من دون آليةٍ منضبطة لحفظ النسخ يصعب معرفة من غيّر البيانات ومتى، وهو أمرٌ جوهري للمساءلة والتدقيق."],
    ["غياب مشاركة المريض", "كثيراً ما يُحرَم المريض من الاطّلاع على سجلّه الصحّي ومتابعة مواعيده وأدويته، فيضعف انخراطه في رعايته."],
    ["الأمان والخصوصية", "البيانات الصحية من أكثر البيانات حساسية، وتتطلّب تحكّماً دقيقاً بالوصول بحسب دور كلّ مستخدم."],
  ];
  // layout: 3 top, 2 bottom
  const cw3 = 3.9, gap = 0.22, y0 = 1.75, hh = 2.28;
  items.slice(0, 3).forEach(([t, d], i) => {
    const x = W - MX - cw3 - i * (cw3 + gap);
    card(s, x, y0, cw3, hh);
    s.addText(t, ar({ x: x + 0.28, y: y0 + 0.2, w: cw3 - 0.56, h: 0.75, fontSize: 14.5, bold: true, color: C.teal, valign: "top", margin: 0 }));
    s.addText(d, ar({ x: x + 0.28, y: y0 + 0.95, w: cw3 - 0.56, h: hh - 1.1, fontSize: 11.5, color: C.muted, lineSpacingMultiple: 1.2, valign: "top", margin: 0 }));
  });
  const cw2 = 5.96, y1 = y0 + hh + 0.3;
  items.slice(3).forEach(([t, d], i) => {
    const x = W - MX - cw2 - i * (cw2 + gap);
    card(s, x, y1, cw2, 2.0);
    s.addText(t, ar({ x: x + 0.28, y: y1 + 0.2, w: cw2 - 0.56, h: 0.45, fontSize: 14.5, bold: true, color: C.teal, valign: "top", margin: 0 }));
    s.addText(d, ar({ x: x + 0.28, y: y1 + 0.72, w: cw2 - 0.56, h: 1.1, fontSize: 11.5, color: C.muted, lineSpacingMultiple: 1.2, valign: "top", margin: 0 }));
  });
  s.addNotes("ينطلق المشروع من خمسة تحدّيات: تشتّت البيانات بين أنظمة لا تتبادلها؛ وغياب التنبيه الاستباقي للمخاطر؛ وصعوبة تتبّع من غيّر ماذا ومتى؛ وحرمان المريض من سجلّه؛ وحساسية البيانات الصحية أمنياً.");
}

// =========================================================================
// 5 — GOALS
// =========================================================================
{
  const s = newSlide();
  header(s, "أهداف المشروع", "هدفٌ عام يتفرّع إلى خمسة أهداف تفصيلية");
  const goals = [
    "بناء سجلٍّ طبي قائم على معيار FHIR R5 يدعم الإنشاء والقراءة والتحديث والحذف والبحث، مع حفظ تاريخ النسخ والتحقّق من صحّة الموارد.",
    "توفير مصادقةٍ وتفويضٍ آمنَين قائمَين على الأدوار ونطاقات الصلاحيات على غرار SMART on FHIR، بحيث يرى كلّ مستخدمٍ ما يخصّه فقط.",
    "تقديم دعمٍ ذكي للقرار السريري عبر محرّك قواعد مرن يُطلق بطاقات تنبيهٍ مبنية على بيانات المريض ومعرفة الأدوية ومخاطره.",
    "دمج نماذج ذكاءٍ اصطناعي تنبّؤية تعمل على بيانات المريض الحقيقية، مع إتاحة ربط نماذج خارجية متخصّصة.",
    "تمكين المريض من الاطّلاع الآمن على سجلّه الصحّي عبر بوابة خدمةٍ ذاتية، بواجهةٍ ثنائية اللغة (عربي / إنجليزي).",
  ];
  goals.forEach((g, i) => {
    const y = 1.72 + i * 1.02;
    card(s, MX, y, CW, 0.88, { fill: i % 2 ? C.white : C.card, line: C.line });
    circleNum(s, String(i + 1), W - MX - 0.98, y + 0.17, 0.54, C.teal);
    s.addText(g, ar({ x: MX + 0.25, y, w: CW - 1.5, h: 0.88, fontSize: 13.5, valign: "middle", lineSpacingMultiple: 1.15, margin: 0 }));
  });
  s.addNotes("الهدف العام: نظامٌ حديث ومتكامل لإدارة السجلات الطبية. ويتفرّع إلى خمسة أهداف: سجلّ FHIR كامل الوظائف، ومصادقة وتفويض بنطاقات صلاحيات، ودعم قرارٍ سريري بمحرّك قواعد، ونماذج ذكاء اصطناعي تنبّؤية، وبوابة مريض ثنائية اللغة.");
}

// =========================================================================
// 6 — FEATURES (8 tiles)
// =========================================================================
{
  const s = newSlide();
  header(s, "أهمّ ميزات المشروع", "من السجل السريري إلى الذكاء التنبّؤي");
  const feats = [
    ["سجلّ FHIR R5", "١٣ نوعاً من موارد FHIR تُخزَّن بصيغة JSON الأصلية."],
    ["النسخ والتاريخ", "حفظ كامل لتاريخ النسخ مع حذفٍ ناعم يضمن المساءلة."],
    ["البحث والتحقّق", "بحث ببارامترات قياسية وتحقّق من الموارد وفق المواصفة."],
    ["المصادقة والتفويض", "Keycloak ورموز JWT وأدوار ونطاقات SMART."],
    ["دعم القرار السريري", "CDS Hooks ومحرّك قواعد وورشة تأليفٍ ونشر."],
    ["نماذج المخاطر", "السكري والقلب وإعادة الدخول — من بيانات FHIR الفعلية."],
    ["بوابة المريض", "اطّلاعٌ آمن على السجل والمواعيد والملف الشخصي."],
    ["واجهة ثنائية اللغة", "عربي / إنجليزي بتبديلٍ فوري ودعم كامل لاتجاه RTL."],
  ];
  const tw = 2.87, th = 2.28, gp = 0.21;
  feats.forEach(([t, d], i) => {
    const col = i % 4, row = Math.floor(i / 4);
    const x = W - MX - tw - col * (tw + gp);
    const y = 1.75 + row * (th + 0.28);
    card(s, x, y, tw, th);
    s.addShape(pptx.ShapeType.ellipse, { x: x + tw - 0.78, y: y + 0.22, w: 0.5, h: 0.5, fill: { color: row === 0 ? C.teal : C.mint } });
    s.addText(String(i + 1), en({ x: x + tw - 0.78, y: y + 0.22, w: 0.5, h: 0.5, fontSize: 13, bold: true, color: C.white, align: "center", valign: "middle", margin: 0 }));
    s.addText(t, ar({ x: x + 0.22, y: y + 0.85, w: tw - 0.44, h: 0.45, fontSize: 14, bold: true, color: C.deep, margin: 0 }));
    s.addText(d, ar({ x: x + 0.22, y: y + 1.32, w: tw - 0.44, h: 0.85, fontSize: 10.5, color: C.muted, lineSpacingMultiple: 1.15, valign: "top", margin: 0 }));
  });
  s.addNotes("تتوزّع الميزات على ثماني مجموعات: سجلّ FHIR بثلاثة عشر نوع مورد، وتأريخ كامل للنسخ، وبحث وتحقّق قياسيان، ومصادقة وتفويض عبر Keycloak، ودعم قرار سريري قابل للتأليف، وثلاثة نماذج مخاطر، وبوابة مريض، وواجهة عربية/إنجليزية كاملة الاتجاهين.");
}

// =========================================================================
// 7 — TECH STACK
// =========================================================================
{
  const s = newSlide();
  header(s, "التقنيات المستخدمة", "حزمة تقنية حديثة ومفتوحة وقابلة للتوسّع");
  const cols = [
    ["الواجهة الخلفية", ["ASP.NET Core 10 (Web API)", "Entity Framework Core 10", "قاعدة PostgreSQL مع تخزين JSONB", "مكتبة Firely SDK لمعيار HL7 FHIR R5", "Serilog · Swagger / OpenAPI"]],
    ["الواجهة الأمامية", ["Blazor WebAssembly (.NET 10)", "Bootstrap 5", "مكوّنات موارد موحّدة", "ترجمة فورية عربي / إنجليزي", "دعم كامل للاتجاهين RTL / LTR"]],
    ["الأمان والهوية", ["خادم الهوية Keycloak", "بروتوكولا OAuth2 وOpenID Connect", "رموز JWT موقَّعة", "أدوار RBAC", "نطاقات SMART on FHIR"]],
    ["الذكاء الاصطناعي", ["Python + scikit-learn", "تصدير عبر skl2onnx", "تشغيل ONNX Runtime", "بيانات تدريب عامة موثّقة", "بيانات Synthea للعرض"]],
  ];
  const cw = 2.87, gp = 0.21, y0 = 1.8, ch = 4.9;
  cols.forEach(([title, items], i) => {
    const x = W - MX - cw - i * (cw + gp);
    card(s, x, y0, cw, ch, { fill: i === 0 ? C.card : C.card });
    s.addShape(pptx.ShapeType.roundRect, { x: x + 0.25, y: y0 + 0.3, w: cw - 0.5, h: 0.55, rectRadius: 0.09, fill: { color: [C.teal, C.mint, C.deep, C.amber][i] } });
    s.addText(title, arC({ x: x + 0.25, y: y0 + 0.3, w: cw - 0.5, h: 0.55, fontSize: 14.5, bold: true, color: i === 3 ? C.deep : C.white, valign: "middle", margin: 0 }));
    s.addText(
      items.map((t, j) => ({ text: t, options: { bullet: j >= 0 ? { code: "2022", indent: 10 } : false, breakLine: true } })),
      ar({ x: x + 0.22, y: y0 + 1.1, w: cw - 0.44, h: ch - 1.3, fontSize: 12, color: C.ink, paraSpaceAfter: 10, valign: "top", margin: 0 })
    );
  });
  s.addNotes("بُنيت الواجهة الخلفية بإطار ASP.NET Core مع EF Core وقاعدة PostgreSQL ومكتبة Firely لمعيار FHIR R5. أمّا الأمامية فبإطار Blazor WebAssembly. الهوية عبر Keycloak وفق OAuth2 وOpenID Connect. ونماذج الذكاء دُرّبت بلغة Python وصُدّرت إلى ONNX لتعمل داخل الخادم مباشرة.");
}

// =========================================================================
// 8 — ARCHITECTURE
// =========================================================================
{
  const s = newSlide();
  header(s, "البنية العامة للنظام", "ثلاث خدمات، وقاعدتا بيانات، وخادم هوية");
  const iw = 10.9, ih = iw * (530 / 1381); // ≈ 4.18
  framedImage(s, DOCS + "/diagrams/architecture.png", (W - iw) / 2, 1.85, iw, ih);
  s.addText(
    "تُصادِق واجهة Blazor عبر Keycloak ثم تخاطب الواجهة البرمجية عبر HTTPS برموز JWT؛ فتخزّن الموارد في PostgreSQL، وتُحمّل نماذج ONNX محلياً، وتستعلم خدمة معلومات الأدوية، مع إمكانية الربط بنماذج ذكاءٍ خارجية وبمصدر RxNorm الحيّ.",
    arC({ x: MX + 0.7, y: 6.2, w: CW - 1.4, h: 0.8, fontSize: 12.5, color: C.muted, lineSpacingMultiple: 1.25, valign: "top" })
  );
  s.addNotes("البنية من ثلاث خدمات: الواجهة الأمامية، والواجهة البرمجية الرئيسة، وخدمة معلومات الأدوية، إضافةً إلى Keycloak وقاعدتَي بيانات. يجري كلّ اتصال عبر HTTPS برموز JWT، وتعمل نماذج ONNX داخل الخادم نفسه دون خدمة خارجية.");
}

// =========================================================================
// 9 — FHIR LAYER
// =========================================================================
{
  const s = newSlide();
  header(s, "السجلّ الطبي وفق معيار HL7 FHIR", "الأساس الذي بُنيت عليه المنصّة");
  // right column: bullets
  const bx = W - MX - 6.9, bw = 6.9;
  const bullets = [
    "معيار HL7 الحديث لتبادل البيانات الصحية؛ يمثّل المفاهيم السريرية على شكل «موارد» قياسية.",
    "اعتُمد الإصدار الخامس R5 عبر مكتبة Firely .NET SDK مع التحقّق من كلّ مورد عند الكتابة.",
    "تُخزَّن الموارد بصيغة FHIR JSON الأصلية حفاظاً على الدقّة الكاملة للبيانات.",
    "جدول نسخٍ مستقل يمنح أثراً تدقيقياً كاملاً: مَن غيّر، ومتى، وما القيمة السابقة.",
    "فهرس بحثٍ مُسطَّح باسم ResourceIndex يُسرّع البحث ببارامترات FHIR القياسية.",
    "حذفٌ ناعم يُبقي التاريخ محفوظاً ويمنع الضياع غير المقصود للبيانات.",
  ];
  s.addText(
    bullets.map((t) => ({ text: t, options: { bullet: { code: "2022", indent: 12 }, breakLine: true } })),
    ar({ x: bx, y: 1.85, w: bw, h: 4.9, fontSize: 14, paraSpaceAfter: 14, lineSpacingMultiple: 1.2, valign: "top" })
  );
  // left: stat callouts
  const stats = [
    ["13", "نوع مورد FHIR مدعوم", C.teal],
    ["R5", "أحدث إصدارات المعيار", C.mint],
    ["JSON", "تخزينٌ بالصيغة الأصلية", C.amber],
  ];
  stats.forEach(([n, l, col], i) => {
    const y = 1.85 + i * 1.65;
    card(s, MX, y, 4.6, 1.45);
    s.addText(n, en({ x: MX + 0.2, y: y + 0.16, w: 1.9, h: 1.1, fontSize: 40, bold: true, color: col, align: "center", valign: "middle", margin: 0 }));
    s.addText(l, ar({ x: MX + 2.15, y: y + 0.16, w: 2.3, h: 1.1, fontSize: 13.5, bold: true, color: C.deep, valign: "middle", margin: 0 }));
  });
  s.addNotes("اخترنا معيار FHIR لأنه اللغة المشتركة الحديثة للبيانات الصحية. كلّ شيء في النظام «مورد» قياسي: مريض، حالة، ملاحظة، دواء… يُتحقَّق من صحّته عند الكتابة، ويُخزَّن بصيغته الأصلية، مع تاريخ نسخ كامل وفهرس بحثٍ سريع. بهذا يكون النظام قابلاً للتكامل مع أيّ نظامٍ صحي يفهم FHIR.");
}

// =========================================================================
// 10 — CDS LAYER
// =========================================================================
{
  const s = newSlide();
  header(s, "دعم القرار السريري CDS", "تنبيهات قابلة للتهيئة في موضع الرعاية — لا مُبرمَجة مسبقاً");
  const blocks = [
    ["CDS Hooks", "مساراتٌ قياسية تُستدعى عند فعلٍ سريري — كوصف دواء — فتُعيد بطاقات تنبيه."],
    ["محرّك القواعد", "قواعد JSON-Logic تُقيَّم على متغيّرات سريرية ودوائية وقيَم الذكاء الاصطناعي."],
    ["ورشة التأليف", "إنشاء القواعد من قوالب، ثم تحقّق ومعاينة ونشرٌ مُصدَّر النسخ وتفعيل."],
    ["معرفة الأدوية", "فحوص الجرعات القصوى والمواد الخاضعة للرقابة وتكرار المكوّن الدوائي."],
  ];
  const bw = 5.96, bh = 1.62, gp = 0.21;
  blocks.forEach(([t, d], i) => {
    const col = i % 2, row = Math.floor(i / 2);
    const x = W - MX - bw - col * (bw + gp);
    const y = 1.78 + row * (bh + 0.24);
    card(s, x, y, bw, bh);
    s.addText(t, ar({ x: x + 0.28, y: y + 0.16, w: bw - 0.56, h: 0.42, fontSize: 15, bold: true, color: C.teal, margin: 0 }));
    s.addText(d, ar({ x: x + 0.28, y: y + 0.6, w: bw - 0.56, h: 0.9, fontSize: 12, color: C.muted, lineSpacingMultiple: 1.2, valign: "top", margin: 0 }));
  });
  // flow strip (RTL: starts right)
  const fy = 5.6, steps = ["وصف دواء", "استدعاء CDS Hook", "تقييم القواعد", "بطاقة تنبيه للطبيب"];
  const sw = 2.55, sg = 0.65;
  steps.forEach((t, i) => {
    const x = W - MX - sw - i * (sw + sg);
    s.addShape(pptx.ShapeType.roundRect, { x, y: fy, w: sw, h: 0.75, rectRadius: 0.1, fill: { color: i === steps.length - 1 ? C.mint : C.deep } });
    s.addText(t, arC({ x, y: fy, w: sw, h: 0.75, fontSize: 12.5, bold: true, color: C.white, valign: "middle", margin: 0 }));
    if (i < steps.length - 1) {
      s.addText("◀", en({ x: x - sg, y: fy, w: sg, h: 0.75, fontSize: 16, color: C.teal, align: "center", valign: "middle", margin: 0 }));
    }
  });
  s.addNotes("طبقة دعم القرار مبنية على مواصفة CDS Hooks: حين يصف الطبيب دواءً يُستدعى المسار، فيُقيّم محرّك قواعد JSON-Logic القواعد الفعّالة على بيانات المريض ومعرفة الأدوية وقيم الذكاء الاصطناعي، ويُعيد بطاقة تنبيه داخل سير العمل. والأهم: القواعد ليست مُبرمَجة في الشيفرة، بل تُؤلَّف وتُنشَر من واجهة إدارية.");
}

// =========================================================================
// 11 — AI MODELS
// =========================================================================
{
  const s = newSlide();
  header(s, "نماذج الذكاء الاصطناعي التنبّؤية", "مُدرَّبة على بيانات بحثية عامة — تُقيِّم كلّ مريض من بياناته الفعلية");
  const models = [
    ["خطر السكري", "Pima Indians Diabetes", "الغلوكوز · الضغط الانبساطي · كتلة الجسم · العمر", "0.82"],
    ["خطر أمراض القلب", "UCI Heart Disease", "العمر · الجنس · الضغط · الكوليسترول · النبض الأقصى", "0.93"],
    ["خطر إعادة الدخول (٣٠ يوماً)", "UCI 130-US Hospitals", "العمر · الجنس · عدد الحالات والأدوية والزيارات والإجراءات", "0.63"],
  ];
  const mw = 3.9, gp = 0.22, y0 = 1.8, mh = 3.6;
  models.forEach(([t, ds, ft, auc], i) => {
    const x = W - MX - mw - i * (mw + gp);
    card(s, x, y0, mw, mh);
    s.addText(t, arC({ x: x + 0.2, y: y0 + 0.22, w: mw - 0.4, h: 0.75, fontSize: 15.5, bold: true, color: C.deep, valign: "top" }));
    s.addText(auc, en({ x: x + 0.2, y: y0 + 0.9, w: mw - 0.4, h: 1.05, fontSize: 48, bold: true, color: [C.teal, C.mint, C.amber][i], align: "center", valign: "middle" }));
    s.addText("ROC-AUC", en({ x: x + 0.2, y: y0 + 1.95, w: mw - 0.4, h: 0.3, fontSize: 10.5, bold: true, color: C.muted, align: "center" }));
    s.addText(ft, arC({ x: x + 0.25, y: y0 + 2.32, w: mw - 0.5, h: 0.75, fontSize: 11, color: C.ink, lineSpacingMultiple: 1.2, valign: "top" }));
    s.addText(ds, en({ x: x + 0.2, y: y0 + 3.12, w: mw - 0.4, h: 0.3, fontSize: 10, italic: true, color: C.muted, align: "center" }));
  });
  card(s, MX, 5.65, CW, 0.95, { fill: C.card });
  s.addText(
    "المبدأ التصميمي: تُدرَّب النماذج حصراً على سماتٍ يمكن استخراجها فعلاً من سجلّ FHIR (رموز LOINC للملاحظات)، وتُعوَّض القيم الناقصة بوسيط بيانات التدريب مع الإشارة إلى ذلك صراحةً — فيحصل الطبيب دائماً على تقديرٍ أمين.",
    ar({ x: MX + 0.3, y: 5.65, w: CW - 0.6, h: 0.95, fontSize: 12.5, valign: "middle", lineSpacingMultiple: 1.2, margin: 0 })
  );
  s.addNotes("ثلاثة نماذج غابات عشوائية دُرّبت في Google Colab وصُدّرت إلى ONNX: السكري بدقّة تمييز ٠٫٨٢، والقلب ٠٫٩٣، وإعادة الدخول ٠٫٦٣ — وهو هدفٌ يصعب التنبّؤ به بطبيعته. الفكرة الجوهرية أنّنا درّبنا كلّ نموذج على السمات التي يستطيع النظام فعلاً استخراجها من سجلّ FHIR، لا على كامل أعمدة مجموعة البحث؛ وعند نقص قيمة تُعوَّض بالوسيط ويُنبَّه الطبيب إلى ذلك.");
}

// =========================================================================
// 12 — EXTERNAL AI + SECURITY
// =========================================================================
{
  const s = newSlide();
  header(s, "النماذج الخارجية والأمان", "منظومة مفتوحة للذكاء — وحمايةٌ صارمة للبيانات");
  const colW2 = 5.96, gp = 0.21, y0 = 1.8, ch = 4.7;
  // right column: external AI
  const rx = W - MX - colW2;
  card(s, rx, y0, colW2, ch);
  s.addText("سجلّ النماذج الخارجية", ar({ x: rx + 0.3, y: y0 + 0.22, w: colW2 - 0.6, h: 0.45, fontSize: 16, bold: true, color: C.teal, margin: 0 }));
  s.addText([
    "تسجيل نماذج ذكاءٍ مستضافة خارجياً عبر روابط HTTPS آمنة حصراً.",
    "أنماط مصادقة مرنة: بلا مصادقة، أو مفتاح API، أو رمز Bearer.",
    "تُخزَّن الأسرار مشفّرةً عبر ASP.NET Data Protection ولا تُعاد إلى العملاء أبداً.",
    "مسار قرارٍ قابل للتهيئة لقراءة النتيجة من استجابة JSON.",
    "تشغيل النموذج على مريضٍ محدّد وعرض القرار وزمن الاستجابة.",
  ].map((t) => ({ text: t, options: { bullet: { code: "2022", indent: 12 }, breakLine: true } })),
    ar({ x: rx + 0.3, y: y0 + 0.75, w: colW2 - 0.6, h: ch - 1, fontSize: 12.5, paraSpaceAfter: 12, lineSpacingMultiple: 1.2, valign: "top" }));
  // left column: security
  card(s, MX, y0, colW2, ch);
  s.addText("الأمان والصلاحيات", ar({ x: MX + 0.3, y: y0 + 0.22, w: colW2 - 0.6, h: 0.45, fontSize: 16, bold: true, color: C.teal, margin: 0 }));
  s.addText([
    "مصادقة مركزية عبر Keycloak وفق بروتوكولَي OAuth2 وOpenID Connect.",
    "رموز JWT موقَّعة يتحقّق منها الخادم في كلّ طلب.",
    "تحكّم بالوصول قائم على الأدوار: مدير نظام، مدير مؤسسة، ممارس، مريض.",
    "نطاقات صلاحيات System وUser وPatient على غرار SMART on FHIR.",
    "تشفير الاتصالات عبر HTTPS وربط حساب المريض بسجلّه عبر رمز دعوة.",
  ].map((t) => ({ text: t, options: { bullet: { code: "2022", indent: 12 }, breakLine: true } })),
    ar({ x: MX + 0.3, y: y0 + 0.75, w: colW2 - 0.6, h: ch - 1, fontSize: 12.5, paraSpaceAfter: 12, lineSpacingMultiple: 1.2, valign: "top" }));
  s.addNotes("النظام ليس مغلقاً على نماذجه: سجلٌّ للنماذج الخارجية يتيح ربط أيّ خدمة ذكاءٍ متخصّصة عبر HTTPS مع تشفير الأسرار. وعلى صعيد الأمان: مصادقة مركزية عبر Keycloak، ورموز JWT، وأدوار ونطاقات صلاحيات على غرار SMART on FHIR تضمن أنّ المريض لا يرى إلا سجلّه، والممارس لا يتجاوز صلاحياته.");
}

// =========================================================================
// 13 — USE CASES
// =========================================================================
{
  const s = newSlide();
  header(s, "مخطّط حالات الاستخدام العام", "خمسة فاعلين يتدرّجون في الصلاحيات بالوراثة");
  const ih = 5.15, iw = ih * (587 / 1035); // ≈ 2.92
  framedImage(s, DOCS + "/diagrams/usecase-overview.png", MX + 0.4, 1.8, iw, ih);
  const actors = [
    ["مدير النظام", "أعلى صلاحية: إدارة المؤسسات على مستوى المنصّة، ويرث قدرات مدير المؤسسة."],
    ["مدير المؤسسة", "الكادر والدعوات، وتأليف قواعد دعم القرار، والنماذج الخارجية — ويرث قدرات الممارس."],
    ["الطبيب / الممارس", "المرضى والسجل السريري والطلبات والمواعيد وبطاقات المخاطر والرؤى."],
    ["المريض", "بوابة خدمة ذاتية: السجل الصحّي والمواعيد والملف الشخصي ضمن نطاقه فقط."],
    ["الزائر", "صفحة الترحيب التعريفية وتبديل اللغة وبدء تسجيل الدخول."],
  ];
  const ax = MX + iw + 0.9, aw = W - MX - ax;
  actors.forEach(([t, d], i) => {
    const y = 1.8 + i * 1.03;
    card(s, ax, y, aw, 0.9);
    s.addText([
      { text: t + "  —  ", options: { bold: true, color: C.teal } },
      { text: d, options: { color: C.muted } },
    ], ar({ x: ax + 0.28, y, w: aw - 0.56, h: 0.9, fontSize: 12.5, valign: "middle", lineSpacingMultiple: 1.15, margin: 0 }));
  });
  s.addNotes("حدّدت الدراسة التحليلية خمسة فاعلين تتدرّج صلاحياتهم بالوراثة: الزائر فالمريض فالممارس فمدير المؤسسة فمدير النظام. ولكلّ فاعل مخطّط حالات استخدامٍ تفصيلي في كتاب المشروع.");
}

// =========================================================================
// 14 — ERD
// =========================================================================
{
  const s = newSlide();
  header(s, "مخطّط قاعدة البيانات ERD", "تصميمٌ عام: مستند FHIR في المركز، وحوله جداول الفهرسة والنسخ والدعم");
  const ih = 5.15, iw = ih * (1676 / 2550); // ≈ 3.39
  framedImage(s, DOCS + "/diagrams/erd.png", MX + 0.3, 1.8, iw, ih);
  const notes = [
    "جدول FhirResource يحفظ النسخة الحالية لكلّ مورد: النوع والمعرّف ومستند JSON.",
    "جدول FhirResourceVersion يحفظ تاريخ النسخ كاملاً لكلّ مورد (أثرٌ تدقيقي).",
    "فهرس ResourceIndex فهرس بحثٍ مُسطَّح يُبنى تلقائياً من الموارد.",
    "علاقة CdsRuleDefinition مع CdsRuleVersion تحقّق نشراً مُصدَّر النسخ لقواعد دعم القرار.",
    "جدول MedicineKnowledgeRecord ذاكرةٌ مؤقّتة لمعرفة الأدوية.",
    "جدول ExternalAiModel سجلّ النماذج الخارجية بأسرارٍ مشفّرة.",
    "قاعدة مستقلة DMRSMedicine للأدوية والمكوّنات بعلاقة متعدّد-متعدّد.",
  ];
  const ax = MX + iw + 0.85, aw = W - MX - ax;
  s.addText(
    notes.map((t) => ({ text: t, options: { bullet: { code: "2022", indent: 12 }, breakLine: true } })),
    ar({ x: ax, y: 1.9, w: aw, h: 5.0, fontSize: 13, paraSpaceAfter: 13, lineSpacingMultiple: 1.2, valign: "top" })
  );
  s.addNotes("جوهر التصميم أنّ مستند FHIR نفسه هو مصدر الحقيقة: جدولٌ للنسخة الحالية، وجدولٌ للتاريخ، وفهرسٌ مُسطَّح للبحث. وإلى جانبها جداول دعم القرار بنشرٍ مُصدَّر النسخ، وسجلّ النماذج الخارجية، وقاعدةٌ مستقلة لمعرفة الأدوية.");
}

// =========================================================================
// 15/16/17 — SCREENSHOTS
// =========================================================================
function shotsSlide(title, subtitle, shots, notes) {
  const s = newSlide();
  header(s, title, subtitle);
  const iw = 5.85, ih = iw * (1032 / 1920); // ≈ 3.14
  shots.forEach(([path, label], i) => {
    const x = i === 0 ? W - MX - iw : MX; // first shot on the RIGHT (RTL)
    framedImage(s, path, x, 2.15, iw, ih);
    s.addText(label, arC({ x, y: 2.15 + ih + 0.18, w: iw, h: 0.4, fontSize: 13, bold: true, color: C.teal, margin: 0 }));
  });
  s.addText("يعرض التسجيل العملي هذه الواجهات وغيرها بالتفصيل.", ar({ x: MX, y: 6.35, w: CW, h: 0.35, fontSize: 11.5, italic: true, color: C.muted }));
  s.addNotes(notes);
  return s;
}

shotsSlide(
  "التطبيق العملي — واجهات الطبيب",
  "لوحة عمل، وملف مريضٍ ببطاقات المخاطر الثلاث",
  [
    [DOCS + "/screenshots/doctor/doctordash.png", "لوحة عمل الطبيب مع قائمة المراقبة التنبّؤية"],
    [DOCS + "/screenshots/doctor/patientai.png", "ملف المريض — بطاقات مخاطر الذكاء الاصطناعي"],
  ],
  "هذه لوحة عمل الطبيب: مؤشّرات حيّة وقائمة مراقبة تنبّؤية بالمرضى الأعلى خطراً. وفي ملف المريض تظهر بطاقات المخاطر الثلاث محسوبةً من بياناته الفعلية مع السمات المستخدمة في كلّ تقدير."
);

shotsSlide(
  "التطبيق العملي — دعم القرار والإدارة",
  "ورشة تأليف القواعد، ولوحة مدير المؤسسة",
  [
    [DOCS + "/screenshots/orgadmin/cds.png", "ورشة تأليف قواعد دعم القرار السريري"],
    [DOCS + "/screenshots/orgadmin/orgdash.png", "لوحة مدير المؤسسة وإدارة الكادر"],
  ],
  "في ورشة دعم القرار يُنشئ المدير القاعدة من قالب، ثم يتحقّق ويعاين وينشر ويفعّل. وإلى جانبها لوحة مدير المؤسسة لإدارة الكادر والدعوات."
);

shotsSlide(
  "التطبيق العملي — بوابة المريض والرؤى الذكية",
  "خدمة ذاتية للمريض، ونظرة سكانية على المخاطر",
  [
    [DOCS + "/screenshots/patient/myhealth.png", "بوابة «صحّتي» — سجلّ المريض ضمن نطاقه فقط"],
    [DOCS + "/screenshots/ai/aiinsights.png", "لوحة الرؤى الذكية — توزيع المخاطر وقوائم المراقبة"],
  ],
  "بوابة المريض تتيح له الاطّلاع الآمن على حالاته وأدويته ومواعيده. ولوحة الرؤى الذكية تنتقل من مريضٍ واحد إلى مستوى المجتمع: توزيع المخاطر، وقوائم الأعلى خطراً، وشرح آلية الحساب — والواجهة كلّها ثنائية اللغة."
);

// =========================================================================
// 18 — FUTURE WORK
// =========================================================================
{
  const s = newSlide();
  header(s, "الآفاق المستقبلية", "أساسٌ قابل للبناء عليه");
  const items = [
    ["منظومة اختبارات آلية", "تغطية اختبارية للواجهات البرمجية ومحرّك دعم القرار وخدمات المخاطر."],
    ["النشر السحابي والحاويات", "حاويات Docker وإدارة أسرار وشهادات TLS وخطوط CI/CD."],
    ["بيانات أدوية حيّة", "استبدال المزوّد التجريبي بمصدر RxNorm الحيّ لإثراء الفحوص الدوائية."],
    ["توسيع نماذج الذكاء", "نماذج جديدة وإعادة تدريبٍ على بياناتٍ محلية حقيقية لرفع الدقّة."],
    ["تطبيق موبايل", "تطبيق محمول للمرضى والكادر يعزّز انخراط المريض في رعايته."],
    ["الأمان والتشغيل البيني", "سجلّ تدقيق وتشفير عند التخزين ودعم حِزَم FHIR وSMART الكاملة."],
  ];
  const tw = 3.9, th = 2.15, gp = 0.22;
  items.forEach(([t, d], i) => {
    const col = i % 3, row = Math.floor(i / 3);
    const x = W - MX - tw - col * (tw + gp);
    const y = 1.85 + row * (th + 0.3);
    card(s, x, y, tw, th);
    s.addShape(pptx.ShapeType.ellipse, { x: x + tw - 0.72, y: y + 0.22, w: 0.44, h: 0.44, fill: { color: row === 0 ? C.teal : C.mint } });
    s.addText(t, ar({ x: x + 0.25, y: y + 0.72, w: tw - 0.5, h: 0.5, fontSize: 14.5, bold: true, color: C.deep, margin: 0 }));
    s.addText(d, ar({ x: x + 0.25, y: y + 1.22, w: tw - 0.5, h: 0.85, fontSize: 11.5, color: C.muted, lineSpacingMultiple: 1.2, valign: "top", margin: 0 }));
  });
  s.addNotes("مع اكتمال الوظائف الأساسية تبقى آفاقٌ واسعة: منظومة اختبارات آلية، ونشرٌ سحابي بالحاويات، وربط مصدر أدوية حيّ مثل RxNorm، وتوسيع النماذج وإعادة تدريبها على بيانات محلية، وتطبيق موبايل، وتعزيز الأمان والتشغيل البيني الكامل.");
}

// =========================================================================
// 19 — CONCLUSION (dark)
// =========================================================================
{
  const s = newSlide({ dark: true });
  deco(s);
  s.addText("الخاتمة", arC({ x: MX, y: 0.7, w: CW, h: 0.7, fontSize: 34, bold: true, color: C.white }));
  s.addText(
    "قدّم هذا المشروع نظاماً متكاملاً لإدارة السجلات الطبية الرقمية يجمع بين سجلٍّ قائم على معيار HL7 FHIR R5، ومحرّك دعم قرارٍ سريري قابل للتهيئة، وثلاثة نماذج ذكاءٍ اصطناعي تعمل على بيانات المريض الحقيقية، إضافةً إلى سجلّ النماذج الخارجية وبوابة المريض وواجهةٍ ثنائية اللغة. وبذلك يعالج المشروع تشتّت البيانات وغياب الدعم الاستباقي للقرار، ويثبت عملياً جدوى البناء فوق معيارٍ عالمي، ويضع أساساً تقنياً موثوقاً وقابلاً للتوسّع نحو تطبيقاتٍ صحية أوسع.",
    arC({ x: MX + 0.9, y: 1.85, w: CW - 1.8, h: 2.6, fontSize: 17, color: C.ice, lineSpacingMultiple: 1.5, valign: "top" })
  );
  const chips = ["FHIR R5", "CDS Hooks", "AI Risk", "External AI", "Keycloak / SMART", "Blazor", "عربي / إنجليزي"];
  const chipW = [1.35, 1.6, 1.3, 1.55, 2.15, 1.15, 1.75];
  const totalW = chipW.reduce((a, b) => a + b, 0) + (chips.length - 1) * 0.25;
  let cx = (W + totalW) / 2; // RTL: first chip at the far right
  chips.forEach((t, i) => {
    cx -= chipW[i];
    s.addShape(pptx.ShapeType.roundRect, { x: cx, y: 5.15, w: chipW[i], h: 0.52, rectRadius: 0.26, fill: { color: C.deep2 }, line: { color: C.mint, width: 1 } });
    s.addText(t, arC({ x: cx, y: 5.15, w: chipW[i], h: 0.52, fontSize: 11.5, bold: true, color: C.white, valign: "middle", margin: 0 }));
    cx -= 0.25;
  });
  s.addText("«من سجلٍّ موحّد… إلى قرارٍ سريري أذكى ورعايةٍ استباقية»", arC({ x: MX, y: 6.15, w: CW, h: 0.5, fontSize: 15, italic: true, color: C.amber }));
  s.addNotes("خلاصة القول: عالج المشروع تشتّت البيانات بغياب معيارٍ موحّد عبر FHIR R5، وأضاف فوق السجل طبقتين من الذكاء: قواعد قرارٍ قابلة للتأليف ونماذج تنبّؤية أمينة، وأشرك المريض في رعايته. ومن سجلٍّ موحّد نصل إلى قرارٍ سريري أذكى ورعايةٍ استباقية.");
}

// =========================================================================
// 20 — REFERENCES
// =========================================================================
{
  const s = newSlide();
  header(s, "المراجع", "أبرز المصادر — والقائمة الكاملة (٣٥ مرجعاً) في كتاب المشروع");
  const refsR = [
    "[1] HL7 International — FHIR Release 5 (R5) — hl7.org/fhir",
    "[2] Firely .NET SDK (HL7.Fhir) — docs.fire.ly",
    "[3] Microsoft — ASP.NET Core — learn.microsoft.com/aspnet/core",
    "[5] Microsoft — Entity Framework Core — learn.microsoft.com/ef/core",
    "[6] PostgreSQL — postgresql.org/docs",
    "[7] Microsoft — Blazor — learn.microsoft.com/aspnet/core/blazor",
    "[8] Keycloak — keycloak.org/documentation",
  ];
  const refsL = [
    "[9] ONNX Runtime — onnxruntime.ai",
    "[10] Pedregosa et al. — Scikit-learn, JMLR 2011 — scikit-learn.org",
    "[13] CDS Hooks Specification — cds-hooks.org",
    "[17] MITRE — Synthea Synthetic Patients — synthetichealth.github.io",
    "[19] HL7 — SMART App Launch — hl7.org/fhir/smart-app-launch",
    "[21] JsonLogic — jsonlogic.com",
    "[22] NLM — RxNorm — nlm.nih.gov/research/umls/rxnorm",
  ];
  const colW2 = 5.96;
  s.addText(refsR.map((t) => ({ text: t, options: { breakLine: true } })),
    en({ x: W - MX - colW2, y: 1.9, w: colW2, h: 4.9, fontSize: 12, align: "left", paraSpaceAfter: 14, valign: "top", color: C.ink }));
  s.addText(refsL.map((t) => ({ text: t, options: { breakLine: true } })),
    en({ x: MX, y: 1.9, w: colW2, h: 4.9, fontSize: 12, align: "left", paraSpaceAfter: 14, valign: "top", color: C.ink }));
  s.addNotes("اعتمدنا في المشروع على الوثائق الرسمية للمعايير والأطر المستخدمة، وعلى أوراقٍ ومجموعات بياناتٍ منشورة لتدريب النماذج، والقائمة الكاملة في كتاب المشروع.");
}

// =========================================================================
// 21 — THANKS (dark)
// =========================================================================
{
  const s = newSlide({ dark: true, footer: false });
  deco(s);
  s.addText("شكراً لحسن إصغائكم", arC({ x: MX, y: 2.7, w: CW, h: 1.1, fontSize: 48, bold: true, color: C.white }));
  s.addText("نسعد بأسئلتكم وملاحظاتكم", arC({ x: MX, y: 4.0, w: CW, h: 0.6, fontSize: 20, color: C.mint }));
  s.addText("نظام السجلات الطبية الرقمية — DMRS", arC({ x: MX, y: 6.6, w: CW, h: 0.4, fontSize: 13, color: C.ice }));
  s.addNotes("شكراً لحسن إصغائكم، ونسعد الآن بالإجابة عن أسئلتكم. وسنعرض بعد ذلك التسجيل العملي للنظام.");
}

// pptxgenjs emits an invalid mid-paragraph <a:pPr> before runs that carry
// breakLine; PowerPoint reacts by swallowing spaces at the run boundaries.
// A pPr is only legal as the first child of <a:p> — strip the strays.
async function stripStrayPPr(file) {
  const JSZip = require("jszip");
  const fs = require("fs");
  const zip = await JSZip.loadAsync(fs.readFileSync(file));
  const names = Object.keys(zip.files).filter((n) => /^ppt\/slides\/slide\d+\.xml$/.test(n));
  for (const n of names) {
    const xml = await zip.file(n).async("string");
    const fixed = xml.replace(/(<\/a:r>)<a:pPr[\s\S]*?<\/a:pPr>/g, "$1");
    zip.file(n, fixed);
  }
  fs.writeFileSync(file, await zip.generateAsync({ type: "nodebuffer", compression: "DEFLATE" }));
}

pptx.writeFile({ fileName: OUT })
  .then(() => stripStrayPPr(OUT))
  .then(() => console.log("WROTE:", OUT));
