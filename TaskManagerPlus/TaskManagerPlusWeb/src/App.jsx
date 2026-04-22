import { useEffect, useMemo, useRef, useState } from "react";
import "./App.css";

const DOWNLOADS = {
  installer: "/downloads/TaskManagerPlus-Setup.exe",
  portable: "/downloads/TaskManagerPlus-Portable.zip",
  readme: "/downloads/README.txt",
  sha256: "sha256-placeholder",
};

function App() {
  const year = useMemo(() => new Date().getFullYear(), []);
  const [menuOpen, setMenuOpen] = useState(false);
  const [headerElevate, setHeaderElevate] = useState(false);
  const [copyLabel, setCopyLabel] = useState("Copy SHA256");

  const navMenuRef = useRef(null);
  const navToggleRef = useRef(null);

  useEffect(() => {
    const onScroll = () => setHeaderElevate(window.scrollY > 6);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => {
    const onKeyDown = (e) => {
      if (e.key === "Escape") setMenuOpen(false);
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, []);

  useEffect(() => {
    if (!menuOpen) return undefined;
    const onPointerDown = (e) => {
      const target = e.target;
      if (!(target instanceof Node)) return;
      if (navMenuRef.current?.contains(target)) return;
      if (navToggleRef.current?.contains(target)) return;
      setMenuOpen(false);
    };
    document.addEventListener("pointerdown", onPointerDown);
    return () => document.removeEventListener("pointerdown", onPointerDown);
  }, [menuOpen]);

  const features = [
    {
      title: "Giao diện rõ ràng",
      desc: "Tập trung vào chỉ số quan trọng; bố cục dễ đọc và ít rối để theo dõi nhanh.",
    },
    {
      title: "Phím tắt & thao tác nhanh",
      desc: "Thiết kế hướng workflow — thao tác ít hơn, tốc độ nhiều hơn.",
    },
    {
      title: "Nhẹ & mượt",
      desc: "Ưu tiên hiệu năng, đảm bảo trải nghiệm mượt ngay cả khi máy đang tải nặng.",
    },
    {
      title: "An toàn",
      desc: "Không cần quyền đặc biệt cho tác vụ cơ bản. (Bạn cập nhật theo đúng app.)",
    },
    {
      title: "Thống kê trực quan",
      desc: "Hiển thị mức sử dụng tài nguyên bằng thanh biểu đồ/điểm nhấn dễ hiểu.",
    },
    {
      title: "Tuỳ biến",
      desc: "Màu sắc, theme, hiển thị cột… (placeholder để bạn bổ sung tính năng thực tế).",
    },
  ];

  const steps = [
    {
      title: "Tải xuống",
      desc: "Chọn bản phù hợp ở cuối trang (Windows installer hoặc portable).",
    },
    {
      title: "Cài đặt / chạy",
      desc: "Mở file setup để cài, hoặc giải nén bản portable và chạy trực tiếp.",
    },
    {
      title: "Quản lý tác vụ",
      desc: "Theo dõi tiến trình và tài nguyên, đóng/mở tác vụ theo nhu cầu.",
    },
  ];

  const faq = [
    {
      q: "TaskManagerPlus có miễn phí không?",
      a: "Tuỳ bạn: ghi rõ bản miễn phí / bản pro / giấy phép tại đây.",
    },
    {
      q: "Ứng dụng có cần quyền admin không?",
      a: "Tác vụ cơ bản thường không cần. Một số thao tác nâng cao có thể cần quyền cao hơn.",
    },
    {
      q: "Tôi đặt file download ở đâu?",
      a: "Đặt các file trong thư mục public/downloads của project để link tải xuống hoạt động.",
    },
  ];

  const onCopySha = async () => {
    try {
      await navigator.clipboard.writeText(DOWNLOADS.sha256);
      setCopyLabel("Đã copy!");
      window.setTimeout(() => setCopyLabel("Copy SHA256"), 1200);
    } catch {
      setCopyLabel("Không copy được");
      window.setTimeout(() => setCopyLabel("Copy SHA256"), 1200);
    }
  };

  return (
    <>
      <a className="skip-link" href="#main">
        Bỏ qua điều hướng
      </a>

      <header className="site-header" data-elevate={headerElevate ? "true" : "false"}>
        <div className="container header-inner">
          <a className="brand" href="#top" aria-label="TaskManagerPlus">
            <span className="brand-mark" aria-hidden="true">
              TM+
            </span>
            <span className="brand-text">TaskManagerPlus</span>
          </a>

          <nav className="nav" aria-label="Điều hướng chính">
            <button
              ref={navToggleRef}
              className="nav-toggle"
              type="button"
              aria-expanded={menuOpen ? "true" : "false"}
              aria-controls="navMenu"
              onClick={() => setMenuOpen((v) => !v)}
            >
              <span className="sr-only">Mở menu</span>
              <span className="nav-toggle-bars" aria-hidden="true"></span>
            </button>

            <div
              ref={navMenuRef}
              id="navMenu"
              className="nav-menu"
              data-open={menuOpen ? "true" : undefined}
            >
              <a className="nav-link" href="#features" onClick={() => setMenuOpen(false)}>
                Tính năng
              </a>
              <a className="nav-link" href="#how" onClick={() => setMenuOpen(false)}>
                Cách dùng
              </a>
              <a className="nav-link" href="#faq" onClick={() => setMenuOpen(false)}>
                FAQ
              </a>
              <a className="nav-link nav-cta" href="#download" onClick={() => setMenuOpen(false)}>
                Download
              </a>
            </div>
          </nav>
        </div>
      </header>

      <main id="main">
        <section id="top" className="hero">
          <div className="container hero-grid">
            <div className="hero-copy">
              <p className="eyebrow">Ứng dụng Windows</p>
              <h1>Quản lý tiến trình &amp; tác vụ — nhanh, gọn, dễ nhìn.</h1>
              <p className="lead">
                TaskManagerPlus giúp bạn theo dõi tiến trình, tài nguyên và tác vụ quan trọng theo
                cách trực quan hơn. Tập trung vào những thứ cần thiết, giảm thao tác thừa.
              </p>
              <div className="hero-actions">
                <a className="btn btn-primary" href="#download">
                  Tải xuống
                </a>
                <a className="btn btn-secondary" href="#features">
                  Xem tính năng
                </a>
              </div>

              <div className="hero-badges" role="list" aria-label="Thông tin nhanh">
                <div className="badge" role="listitem">
                  <div className="badge-title">Nhẹ</div>
                  <div className="badge-sub">Tối ưu trải nghiệm</div>
                </div>
                <div className="badge" role="listitem">
                  <div className="badge-title">Nhanh</div>
                  <div className="badge-sub">Phản hồi tức thì</div>
                </div>
                <div className="badge" role="listitem">
                  <div className="badge-title">Trực quan</div>
                  <div className="badge-sub">Thông tin rõ ràng</div>
                </div>
              </div>
            </div>

            <div className="hero-media" aria-label="Minh hoạ giao diện">
              <div className="mock-window" role="img" aria-label="Ảnh minh hoạ giao diện TaskManagerPlus">
                <div className="mock-titlebar" aria-hidden="true">
                  <span className="dot dot-red"></span>
                  <span className="dot dot-yellow"></span>
                  <span className="dot dot-green"></span>
                  <span className="mock-title">TaskManagerPlus</span>
                </div>
                <div className="mock-body" aria-hidden="true">
                  <div className="mock-sidebar">
                    <div className="mock-pill active"></div>
                    <div className="mock-pill"></div>
                    <div className="mock-pill"></div>
                    <div className="mock-pill"></div>
                  </div>
                  <div className="mock-content">
                    <div className="mock-row">
                      <div className="mock-kpi">
                        <div className="mock-kpi-label">CPU</div>
                        <div className="mock-kpi-value">23%</div>
                        <div className="mock-bar">
                          <span style={{ width: "23%" }}></span>
                        </div>
                      </div>
                      <div className="mock-kpi">
                        <div className="mock-kpi-label">RAM</div>
                        <div className="mock-kpi-value">6.4 GB</div>
                        <div className="mock-bar">
                          <span style={{ width: "62%" }}></span>
                        </div>
                      </div>
                    </div>
                    <div className="mock-table">
                      <div className="mock-table-header">
                        <span>Process</span>
                        <span>CPU</span>
                        <span>RAM</span>
                      </div>
                      <div className="mock-table-row">
                        <span>Browser.exe</span>
                        <span>8%</span>
                        <span>1.2 GB</span>
                      </div>
                      <div className="mock-table-row">
                        <span>Editor.exe</span>
                        <span>5%</span>
                        <span>0.8 GB</span>
                      </div>
                      <div className="mock-table-row">
                        <span>Game.exe</span>
                        <span>10%</span>
                        <span>2.4 GB</span>
                      </div>
                      <div className="mock-table-row">
                        <span>BackgroundSvc</span>
                        <span>1%</span>
                        <span>0.1 GB</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              
            </div>
          </div>
        </section>

        <section id="features" className="section">
          <div className="container">
            <div className="section-head">
              <h2>Tính năng nổi bật</h2>
            </div>

            <div className="cards">
              {features.map((f) => (
                <article key={f.title} className="card">
                  <h3>{f.title}</h3>
                  <p>{f.desc}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section id="how" className="section section-alt">
          <div className="container">
            <div className="section-head">
              <h2>Cách dùng nhanh</h2>
              <p className="muted">3 bước đơn giản để bắt đầu.</p>
            </div>

            <ol className="steps">
              {steps.map((s, idx) => (
                <li key={s.title} className="step">
                  <div className="step-num">{idx + 1}</div>
                  <div className="step-body">
                    <h3>{s.title}</h3>
                    <p>{s.desc}</p>
                  </div>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <section id="download" className="section download">
          <div className="container">
            <div className="section-head">
              <h2>Download TaskManagerPlus</h2>
            </div>
            <div className="download-grid">
              <div className="download-card">
                <h3>Windows (x64) — Installer</h3>
                <p className="muted">Khuyến nghị cho đa số người dùng.</p>
                <div className="download-actions">
                  <a className="btn btn-primary" href={DOWNLOADS.installer} download>
                    Tải bản cài đặt
                  </a>
                  
                </div>
                <ul className="download-meta">
                  <li>Yêu cầu: Windows 10/11</li>
                  <li>CPU/RAM: tuỳ máy</li>
                 
                </ul>
              </div>

              <div className="download-card">
                <h3>Windows — Portable</h3>
                <p className="muted">Không cần cài đặt, chạy trực tiếp.</p>
                <div className="download-actions">
                  <a className="btn btn-secondary" href={DOWNLOADS.portable} download>
                    Tải bản portable
                  </a>
                  <button className="btn btn-ghost" type="button" onClick={onCopySha}>
                    {copyLabel}
                  </button>
                </div>
                <ul className="download-meta">
                  <li>
                    Giải nén → chạy <span className="mono">TaskManagerPlus.exe</span>
                  </li>
                  <li>Không ghi registry (tuỳ bạn)</li>
                  <li>Phù hợp USB</li>
                </ul>
              </div>
            </div>

           
          </div>
        </section>

        <section id="faq" className="section">
          <div className="container">
            <div className="section-head">
              <h2>FAQ</h2>
              <p className="muted">Câu hỏi thường gặp</p>
            </div>

            <div className="faq">
              {faq.map((item) => (
                <details key={item.q} className="faq-item">
                  <summary>{item.q}</summary>
                  <p>{item.a}</p>
                </details>
              ))}
            </div>
          </div>
        </section>

        <section className="section cta">
          <div className="container cta-inner">
            <div className="cta-copy">
              <h2>Sẵn sàng dùng TaskManagerPlus?</h2>
              <p className="muted">Nhấn tải xuống và bắt đầu ngay — nhanh gọn, không rườm rà.</p>
            </div>
            <div className="cta-actions">
              <a className="btn btn-primary" href="#download">
                Download
              </a>
              <a className="btn btn-secondary" href="#top">
                Lên đầu trang
              </a>
            </div>
          </div>
        </section>
      </main>

     
    </>
  );
}

export default App;
