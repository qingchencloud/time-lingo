const translations = {
  zh: {
    "nav.features": "功能",
    "nav.download": "下载",
    "nav.faq": "常见问题",
    "nav.get": "下载",
    "hero.eyebrow": "Windows 悬浮世界时间 + 快速翻译",
    "hero.title": "随时看对时区，顺手完成翻译。",
    "hero.copy": "TimeLingo 是一个很小的 Windows 小工具，适合跨语言、跨时区工作的人。一个 exe，悬浮时钟，常用时区，多语言快速翻译。",
    "hero.download": "下载 Windows 版",
    "hero.github": "查看 GitHub",
    "proof.exe": "单个 exe",
    "proof.free": "开源",
    "proof.tray": "支持托盘",
    "proof.update": "在线更新",
    "features.eyebrow": "日常可用",
    "features.title": "一个可以一直放在桌面的轻量工具。",
    "feature.time.title": "常用时区预设",
    "feature.time.copy": "支持北京、UTC、美国太平洋、美国东部、伦敦、东京、新加坡、悉尼等常用时区。",
    "feature.translate.title": "多语言翻译",
    "feature.translate.copy": "支持常用语言方向，自动判断输入语言，并可设置默认目标语言。",
    "feature.simple.title": "简单的 Windows 工作流",
    "feature.simple.copy": "支持置顶、系统托盘、开机自启、夜间模式，以及 GitHub 在线更新。",
    "seo.eyebrow": "为什么用 TimeLingo",
    "seo.title": "世界时间和翻译，放在一个不打扰的小窗口里。",
    "seo.copy1": "很多翻译工具都要打开浏览器标签页，很多世界时钟又是另一个应用。TimeLingo 把两个高频动作放到一个轻量悬浮窗口里。",
    "seo.copy2": "它适合远程办公、海外账号、跨境团队、旅行规划、客服回复和快速多语言写作。",
    "download.eyebrow": "下载",
    "download.title": "一个 Windows exe，不需要压缩包。",
    "download.copy": "可以直接试用，也可以通过首次启动安装引导创建桌面图标和开机自启。",
    "download.cta": "下载 TimeLingo.exe",
    "download.release": "查看最新版",
    "faq.eyebrow": "FAQ",
    "faq.title": "下载前的几个问题。",
    "faq.q1": "TimeLingo 会修改系统时区吗？",
    "faq.a1": "不会。它只在应用里显示你选择的时区。",
    "faq.q2": "只能中英文互译吗？",
    "faq.a2": "不是。中英文是常用默认场景，TimeLingo 也支持其他常用语言方向。",
    "faq.q3": "为什么翻译有时会慢？",
    "faq.a3": "默认公共翻译接口适合轻量使用。长期稳定使用可以配置 Microsoft Translator 或 DeepL。",
    "footer.github": "GitHub 仓库"
  },
  en: {}
};

const toggle = document.querySelector("[data-lang-toggle]");
const savedLang = localStorage.getItem("timelingo_lang");
const queryLang = new URLSearchParams(window.location.search).get("lang");
let currentLang = queryLang === "zh-CN" || savedLang === "zh" ? "zh" : "en";

function applyLanguage(lang) {
  currentLang = lang;
  document.documentElement.lang = lang === "zh" ? "zh-CN" : "en";
  document.body.classList.toggle("zh", lang === "zh");
  document.title = lang === "zh"
    ? "TimeLingo - Windows 世界时间和多语言翻译工具"
    : "TimeLingo - World Time and Translation Tool for Windows";

  document.querySelectorAll("[data-i18n]").forEach((node) => {
    const key = node.getAttribute("data-i18n");
    const value = translations[lang][key];
    if (value) node.textContent = value;
  });

  if (toggle) {
    toggle.textContent = lang === "zh" ? "EN" : "中文";
    toggle.setAttribute("aria-label", lang === "zh" ? "Switch to English" : "切换到中文");
  }

  localStorage.setItem("timelingo_lang", lang);
}

if (toggle) {
  toggle.addEventListener("click", () => {
    applyLanguage(currentLang === "zh" ? "en" : "zh");
  });
}

applyLanguage(currentLang);
