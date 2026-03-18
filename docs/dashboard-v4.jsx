import { useState, useEffect, useRef, useCallback, useMemo } from "react";
import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer } from "recharts";

/* ═══════════════════════════════════════════════════════════════════
   DATA
   ═══════════════════════════════════════════════════════════════════ */
const HISTORY = Array.from({ length: 90 }, (_, i) => {
  const base = 124000, trend = i * 220;
  const noise = Math.sin(i * 0.3) * 3200 + Math.cos(i * 0.7) * 1800 + (Math.random() - 0.4) * 2400;
  return { date: new Date(2026, 0, i + 1).toLocaleDateString("en-US", { month: "short", day: "numeric" }), value: Math.round(base + trend + noise) };
});

const WATCHLIST = [
  { ticker: "AAPL", name: "Apple Inc.", price: 247.63, change: 3.42, pct: 1.40, high: 251.20, low: 244.18, open: 244.88, vol: "62.1M" },
  { ticker: "NVDA", name: "NVIDIA Corp.", price: 892.14, change: -12.87, pct: -1.42, high: 908.40, low: 886.50, open: 904.90, vol: "48.3M" },
  { ticker: "MSFT", name: "Microsoft Corp.", price: 468.21, change: 5.18, pct: 1.12, high: 472.00, low: 462.14, open: 463.10, vol: "22.7M" },
  { ticker: "AMZN", name: "Amazon.com", price: 218.47, change: -1.93, pct: -0.88, high: 222.10, low: 217.00, open: 220.38, vol: "35.2M" },
  { ticker: "GOOGL", name: "Alphabet Inc.", price: 186.34, change: 2.11, pct: 1.15, high: 188.80, low: 183.90, open: 184.22, vol: "19.8M" },
  { ticker: "TSLA", name: "Tesla Inc.", price: 342.18, change: -6.42, pct: -1.84, high: 352.00, low: 338.60, open: 348.55, vol: "71.9M" },
  { ticker: "META", name: "Meta Platforms", price: 612.50, change: 8.74, pct: 1.45, high: 618.30, low: 602.40, open: 603.82, vol: "15.4M" },
  { ticker: "JPM", name: "JPMorgan Chase", price: 243.67, change: 1.89, pct: 0.78, high: 245.50, low: 241.20, open: 241.80, vol: "8.2M" },
];

// Per-stock chart history (90 days, keyed by ticker)
const STOCK_HISTORIES = {};
WATCHLIST.forEach((s) => {
  const base = s.price * 0.82;
  const trend = (s.price - base) / 90;
  STOCK_HISTORIES[s.ticker] = Array.from({ length: 90 }, (_, i) => {
    const noise = Math.sin(i * 0.25 + s.ticker.charCodeAt(0)) * s.price * 0.03
      + Math.cos(i * 0.6 + s.ticker.charCodeAt(1)) * s.price * 0.02
      + (Math.random() - 0.45) * s.price * 0.015;
    return {
      date: new Date(2026, 0, i + 1).toLocaleDateString("en-US", { month: "short", day: "numeric" }),
      value: Math.round((base + trend * i + noise) * 100) / 100,
    };
  });
});

const HOLDINGS = [
  { ticker: "AAPL", shares: 85, avg: 178.40, current: 247.63 },
  { ticker: "NVDA", shares: 22, avg: 480.00, current: 892.14 },
  { ticker: "MSFT", shares: 40, avg: 380.50, current: 468.21 },
  { ticker: "GOOGL", shares: 60, avg: 142.20, current: 186.34 },
  { ticker: "META", shares: 18, avg: 350.00, current: 612.50 },
  { ticker: "JPM", shares: 30, avg: 195.00, current: 243.67 },
];

const SECTORS = [
  { name: "Technology", pct: 68.2, color: "#2d6a4f" },
  { name: "Consumer Disc.", pct: 14.5, color: "#c9a96e" },
  { name: "Financials", pct: 9.8, color: "#6b705c" },
  { name: "Healthcare", pct: 4.8, color: "#a68a64" },
  { name: "Energy", pct: 2.7, color: "#b7b7a4" },
];

const VOLUME = Array.from({ length: 24 }, (_, i) => ({
  h: `${i}:00`,
  v: i >= 9 && i <= 16 ? Math.round(40 + Math.random() * 55 + (i === 10 || i === 15 ? 28 : 0)) : Math.round(5 + Math.random() * 12),
}));

const NEWS = [
  { time: "2m", text: "Fed signals potential rate adjustment in Q2 meeting minutes", tag: "Macro" },
  { time: "18m", text: "NVIDIA beats earnings expectations, raises datacenter guidance", tag: "Earnings" },
  { time: "34m", text: "Treasury yields climb as inflation data exceeds forecast", tag: "Bonds" },
  { time: "1h", text: "Apple announces expanded AI features across product lineup", tag: "Tech" },
];

const TF = ["1D", "1W", "1M", "3M", "YTD", "1Y"];
const B_LEVELS = ["⠀", "⣀", "⣤", "⣴", "⣶", "⣷", "⣿"];
const B_SPIN = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
const B_HEART = ["⠀", "⠀", "⣀", "⣠", "⣴", "⣶", "⣿", "⣶", "⣴", "⣠", "⣀", "⠀", "⠀", "⠀", "⠀", "⠀"];

const STREAM_TICKERS = [
  { t: "AAPL", p: "247.63", d: "+1.40", up: true },
  { t: "NVDA", p: "892.14", d: "−1.42", up: false },
  { t: "MSFT", p: "468.21", d: "+1.12", up: true },
  { t: "AMZN", p: "218.47", d: "−0.88", up: false },
  { t: "GOOGL", p: "186.34", d: "+1.15", up: true },
  { t: "TSLA", p: "342.18", d: "−1.84", up: false },
  { t: "META", p: "612.50", d: "+1.45", up: true },
  { t: "JPM", p: "243.67", d: "+0.78", up: true },
  { t: "V", p: "312.88", d: "+0.62", up: true },
  { t: "BRK.B", p: "478.20", d: "+0.31", up: true },
  { t: "UNH", p: "521.74", d: "−0.55", up: false },
  { t: "XOM", p: "118.42", d: "+0.93", up: true },
];


/* ═══════════════════════════════════════════════════════════════════
   ANIMATION HOOKS
   ═══════════════════════════════════════════════════════════════════ */
function useSpring(target, stiffness = 0.08, damping = 0.82) {
  const ref = useRef({ value: 0, velocity: 0 });
  const [value, setValue] = useState(0);
  useEffect(() => {
    let raf;
    const step = () => {
      const s = ref.current;
      const force = (target - s.value) * stiffness;
      s.velocity = (s.velocity + force) * damping;
      s.value += s.velocity;
      if (Math.abs(s.velocity) > 0.001 || Math.abs(target - s.value) > 0.01) {
        setValue(s.value);
        raf = requestAnimationFrame(step);
      } else {
        s.value = target;
        setValue(target);
      }
    };
    raf = requestAnimationFrame(step);
    return () => cancelAnimationFrame(raf);
  }, [target, stiffness, damping]);
  return value;
}

function useMouse(ref) {
  const [pos, setPos] = useState({ x: 0.5, y: 0.5, active: false });
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const onMove = (e) => {
      const r = el.getBoundingClientRect();
      setPos({ x: (e.clientX - r.left) / r.width, y: (e.clientY - r.top) / r.height, active: true });
    };
    const onLeave = () => setPos((p) => ({ ...p, active: false }));
    el.addEventListener("mousemove", onMove);
    el.addEventListener("mouseleave", onLeave);
    return () => { el.removeEventListener("mousemove", onMove); el.removeEventListener("mouseleave", onLeave); };
  }, [ref]);
  return pos;
}


/* ═══════════════════════════════════════════════════════════════════
   ROLLING ODOMETER — Each digit independently rolls to its target
   ═══════════════════════════════════════════════════════════════════ */
function Odometer({ value, prefix = "$", fontSize = 62 }) {
  const formatted = value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const chars = (prefix + formatted).split("");

  return (
    <span style={{
      display: "inline-flex", fontFamily: "var(--serif)", fontSize, fontWeight: 400,
      lineHeight: 1, letterSpacing: "-0.03em", color: "var(--text)", overflow: "hidden",
    }}>
      {chars.map((ch, i) => {
        const isDigit = /\d/.test(ch);
        if (!isDigit) {
          return (
            <span key={`s-${i}`} style={{
              display: "inline-block",
              animation: `odoFadeIn 0.6s ease ${0.8 + i * 0.03}s both`,
              width: ch === "," ? "0.3em" : undefined,
            }}>
              {ch}
            </span>
          );
        }
        const digit = parseInt(ch, 10);
        return <OdometerDigit key={`d-${i}`} digit={digit} delay={0.8 + i * 0.06} fontSize={fontSize} />;
      })}
    </span>
  );
}

function OdometerDigit({ digit, delay, fontSize }) {
  const [arrived, setArrived] = useState(false);
  useEffect(() => {
    const t = setTimeout(() => setArrived(true), delay * 1000);
    return () => clearTimeout(t);
  }, [delay]);

  // Roll through a few random digits before landing on target
  const rollHeight = fontSize * 1.05;
  const totalDigits = digit + 10; // roll through full cycle + target offset
  const stripHeight = totalDigits * rollHeight;

  return (
    <span style={{
      display: "inline-block", height: `${rollHeight}px`, overflow: "hidden",
      width: "0.62em", position: "relative",
    }}>
      <span style={{
        display: "flex", flexDirection: "column", position: "absolute",
        top: arrived ? `${-(digit) * rollHeight}px` : `${-stripHeight + rollHeight}px`,
        transition: arrived
          ? `top 1.4s cubic-bezier(0.16, 1, 0.3, 1)`
          : "none",
        willChange: "top",
      }}>
        {Array.from({ length: totalDigits + 1 }, (_, i) => (
          <span key={i} style={{
            display: "block", height: rollHeight, lineHeight: `${rollHeight}px`,
            textAlign: "center",
          }}>
            {i % 10}
          </span>
        ))}
      </span>
    </span>
  );
}


/* ═══════════════════════════════════════════════════════════════════
   AMBIENT FLOATING ORBS
   ═══════════════════════════════════════════════════════════════════ */
function AmbientOrbs() {
  const orbs = useMemo(() => [
    { x: "15%", y: "20%", size: 300, color: "rgba(45,106,79,0.04)", dur: "28s", delay: "0s" },
    { x: "75%", y: "15%", size: 250, color: "rgba(201,169,110,0.05)", dur: "34s", delay: "-8s" },
    { x: "50%", y: "70%", size: 350, color: "rgba(45,106,79,0.03)", dur: "40s", delay: "-15s" },
    { x: "85%", y: "60%", size: 200, color: "rgba(201,169,110,0.04)", dur: "24s", delay: "-5s" },
  ], []);
  return (
    <div style={{ position: "fixed", inset: 0, pointerEvents: "none", zIndex: 0, overflow: "hidden" }}>
      {orbs.map((o, i) => (
        <div key={i} style={{
          position: "absolute", left: o.x, top: o.y,
          width: o.size, height: o.size, borderRadius: "50%",
          background: `radial-gradient(circle, ${o.color} 0%, transparent 70%)`,
          animation: `orbFloat ${o.dur} ease-in-out ${o.delay} infinite`,
          willChange: "transform",
        }} />
      ))}
    </div>
  );
}


/* ═══════════════════════════════════════════════════════════════════
   RIPPLE EFFECT — Expands from click point on any card
   ═══════════════════════════════════════════════════════════════════ */
function useRipple() {
  const [ripples, setRipples] = useState([]);
  const trigger = useCallback((e) => {
    const r = e.currentTarget.getBoundingClientRect();
    const x = e.clientX - r.left;
    const y = e.clientY - r.top;
    const id = Date.now();
    setRipples((prev) => [...prev, { id, x, y }]);
    setTimeout(() => setRipples((prev) => prev.filter((r) => r.id !== id)), 700);
  }, []);

  const RippleLayer = useCallback(() => (
    <div style={{ position: "absolute", inset: 0, overflow: "hidden", borderRadius: "inherit", pointerEvents: "none", zIndex: 0 }}>
      {ripples.map((r) => (
        <span key={r.id} style={{
          position: "absolute", left: r.x, top: r.y,
          width: 300, height: 300,
          borderRadius: "50%",
          background: "radial-gradient(circle, rgba(201,169,110,0.12) 0%, transparent 70%)",
          transform: "translate(-50%, -50%) scale(0)",
          animation: "rippleExpand 0.7s ease-out forwards",
        }} />
      ))}
    </div>
  ), [ripples]);

  return { trigger, RippleLayer };
}


/* ═══════════════════════════════════════════════════════════════════
   TRADE DRAWER — Slide-in panel with order form
   ═══════════════════════════════════════════════════════════════════ */
function TradeDrawer({ stock, onClose }) {
  const [side, setSide] = useState("buy");
  const [qty, setQty] = useState("");
  const [orderType, setOrderType] = useState("market");
  const [limitPrice, setLimitPrice] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [closing, setClosing] = useState(false);

  const price = stock?.price || 0;
  const numQty = parseInt(qty, 10) || 0;
  const execPrice = orderType === "market" ? price : (parseFloat(limitPrice) || price);
  const total = numQty * execPrice;
  const isBuy = side === "buy";

  const handleClose = useCallback(() => {
    setClosing(true);
    setTimeout(() => { setClosing(false); onClose(); }, 300);
  }, [onClose]);

  const handleConfirm = useCallback(() => {
    setConfirmed(true);
    setTimeout(() => { setConfirmed(false); handleClose(); }, 1800);
  }, [handleClose]);

  if (!stock) return null;

  return (
    <>
      {/* Backdrop */}
      <div onClick={handleClose} style={{
        position: "fixed", inset: 0, zIndex: 1000,
        background: "rgba(26,26,46,0.25)", backdropFilter: "blur(4px)",
        animation: closing ? "fadeOut 0.3s ease forwards" : "fadeIn 0.2s ease",
      }} />

      {/* Drawer */}
      <div style={{
        position: "fixed", top: 0, right: 0, bottom: 0, width: 420, zIndex: 1001,
        background: "var(--card)", borderLeft: "1px solid var(--border)",
        boxShadow: "-20px 0 60px rgba(0,0,0,0.08)",
        animation: closing ? "drawerSlideOut 0.3s ease forwards" : "drawerSlideIn 0.35s cubic-bezier(0.34, 1.56, 0.64, 1)",
        display: "flex", flexDirection: "column",
        fontFamily: "var(--sans)",
      }}>
        {/* Header */}
        <div style={{
          padding: "24px 28px 20px", borderBottom: "1px solid var(--border)",
          display: "flex", justifyContent: "space-between", alignItems: "flex-start",
        }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 4 }}>
              <span style={{ fontFamily: "var(--serif)", fontSize: 24, fontWeight: 400 }}>{stock.ticker}</span>
              <span style={{
                fontSize: 9, fontWeight: 600, padding: "3px 8px", borderRadius: 6,
                background: stock.pct >= 0 ? "rgba(45,106,79,0.08)" : "rgba(181,52,43,0.08)",
                color: stock.pct >= 0 ? "var(--gain)" : "var(--loss)",
                letterSpacing: "0.04em",
              }}>{stock.pct >= 0 ? "+" : ""}{stock.pct}%</span>
            </div>
            <div style={{ fontSize: 12, color: "var(--muted)" }}>{stock.name}</div>
            <div style={{ fontFamily: "var(--mono)", fontSize: 20, fontWeight: 600, marginTop: 8 }}>
              ${price.toFixed(2)}
            </div>
          </div>
          <button onClick={handleClose} style={{
            width: 32, height: 32, borderRadius: 8, border: "1px solid var(--border)",
            background: "transparent", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--muted)", fontSize: 16, transition: "all 0.2s",
          }}
          onMouseEnter={(e) => { e.currentTarget.style.background = "#f8f6f2"; e.currentTarget.style.borderColor = "var(--subtle)"; }}
          onMouseLeave={(e) => { e.currentTarget.style.background = "transparent"; e.currentTarget.style.borderColor = "var(--border)"; }}>
            ×
          </button>
        </div>

        {confirmed ? (
          /* Success state */
          <div style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 16, padding: 40 }}>
            <div style={{
              width: 56, height: 56, borderRadius: "50%",
              background: isBuy ? "rgba(45,106,79,0.1)" : "rgba(181,52,43,0.1)",
              display: "flex", alignItems: "center", justifyContent: "center",
              animation: "cardEntrance 0.5s cubic-bezier(0.34, 1.56, 0.64, 1)",
            }}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke={isBuy ? "var(--gain)" : "var(--loss)"} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="20 6 9 17 4 12" />
              </svg>
            </div>
            <div style={{ textAlign: "center" }}>
              <div style={{ fontFamily: "var(--serif)", fontSize: 20, marginBottom: 4 }}>Order Placed</div>
              <div style={{ fontSize: 13, color: "var(--muted)" }}>
                {isBuy ? "Buy" : "Sell"} {numQty} {stock.ticker} @ {orderType === "market" ? "Market" : `$${execPrice.toFixed(2)}`}
              </div>
            </div>
          </div>
        ) : (
          /* Order form */
          <div style={{ flex: 1, overflow: "auto", padding: "24px 28px" }}>
            {/* Buy/Sell Toggle */}
            <div style={{
              display: "grid", gridTemplateColumns: "1fr 1fr", gap: 6,
              background: "#f6f4f0", borderRadius: 12, padding: 4, marginBottom: 24,
            }}>
              {["buy", "sell"].map((s) => (
                <button key={s} onClick={() => setSide(s)} style={{
                  padding: "10px 0", border: "none", borderRadius: 10, cursor: "pointer",
                  fontFamily: "var(--sans)", fontSize: 13, fontWeight: 600, letterSpacing: "0.02em",
                  textTransform: "capitalize",
                  background: side === s ? "var(--card)" : "transparent",
                  color: side === s ? (s === "buy" ? "var(--gain)" : "var(--loss)") : "var(--subtle)",
                  boxShadow: side === s ? "0 2px 8px rgba(0,0,0,0.06)" : "none",
                  transition: "all 0.25s ease",
                }}>{s}</button>
              ))}
            </div>

            {/* Quantity */}
            <div style={{ marginBottom: 20 }}>
              <label style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.1em", display: "block", marginBottom: 8 }}>
                Shares
              </label>
              <input type="number" value={qty} onChange={(e) => setQty(e.target.value)} placeholder="0"
                min="1" step="1"
                style={{
                  width: "100%", padding: "12px 16px", borderRadius: 10,
                  border: "1px solid var(--border)", background: "#faf8f5",
                  fontFamily: "var(--mono)", fontSize: 18, fontWeight: 600, color: "var(--text)",
                  outline: "none", transition: "border-color 0.2s",
                }}
                onFocus={(e) => e.target.style.borderColor = "var(--accent)"}
                onBlur={(e) => e.target.style.borderColor = "var(--border)"}
              />
              {/* Quick amounts */}
              <div style={{ display: "flex", gap: 6, marginTop: 8 }}>
                {[1, 5, 10, 25, 100].map((n) => (
                  <button key={n} onClick={() => setQty(String(n))} style={{
                    flex: 1, padding: "6px 0", borderRadius: 8, border: "1px solid var(--border)",
                    background: qty === String(n) ? "#f0ece6" : "transparent",
                    fontFamily: "var(--mono)", fontSize: 11, fontWeight: 500, color: "var(--text)",
                    cursor: "pointer", transition: "all 0.2s",
                  }}>{n}</button>
                ))}
              </div>
            </div>

            {/* Order Type */}
            <div style={{ marginBottom: 20 }}>
              <label style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.1em", display: "block", marginBottom: 8 }}>
                Order Type
              </label>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 6 }}>
                {[
                  { key: "market", label: "Market" },
                  { key: "limit", label: "Limit" },
                  { key: "stop", label: "Stop" },
                ].map((o) => (
                  <button key={o.key} onClick={() => setOrderType(o.key)} style={{
                    padding: "10px 0", borderRadius: 10, cursor: "pointer",
                    border: `1px solid ${orderType === o.key ? "var(--accent)" : "var(--border)"}`,
                    background: orderType === o.key ? "rgba(201,169,110,0.06)" : "transparent",
                    fontFamily: "var(--sans)", fontSize: 12, fontWeight: 600,
                    color: orderType === o.key ? "var(--accent)" : "var(--muted)",
                    transition: "all 0.2s",
                  }}>{o.label}</button>
                ))}
              </div>
            </div>

            {/* Limit/Stop price */}
            {orderType !== "market" && (
              <div style={{
                marginBottom: 20,
                animation: "fadeUp 0.3s ease",
              }}>
                <label style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.1em", display: "block", marginBottom: 8 }}>
                  {orderType === "limit" ? "Limit" : "Stop"} Price
                </label>
                <input type="number" value={limitPrice} onChange={(e) => setLimitPrice(e.target.value)}
                  placeholder={price.toFixed(2)} step="0.01"
                  style={{
                    width: "100%", padding: "12px 16px", borderRadius: 10,
                    border: "1px solid var(--border)", background: "#faf8f5",
                    fontFamily: "var(--mono)", fontSize: 16, fontWeight: 500, color: "var(--text)",
                    outline: "none", transition: "border-color 0.2s",
                  }}
                  onFocus={(e) => e.target.style.borderColor = "var(--accent)"}
                  onBlur={(e) => e.target.style.borderColor = "var(--border)"}
                />
              </div>
            )}

            {/* Order Summary */}
            {numQty > 0 && (
              <div style={{
                padding: "16px 18px", borderRadius: 12, background: "#faf8f5",
                border: "1px solid var(--border)", marginBottom: 20,
                animation: "fadeUp 0.25s ease",
              }}>
                <div style={{ fontSize: 10, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.1em", marginBottom: 12 }}>
                  Order Preview
                </div>
                {[
                  { label: "Action", val: `${isBuy ? "Buy" : "Sell"} ${stock.ticker}` },
                  { label: "Quantity", val: `${numQty} shares` },
                  { label: "Price", val: orderType === "market" ? `~$${price.toFixed(2)} (Market)` : `$${execPrice.toFixed(2)} (${orderType})` },
                ].map((r) => (
                  <div key={r.label} style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
                    <span style={{ fontSize: 12, color: "var(--muted)" }}>{r.label}</span>
                    <span style={{ fontSize: 12, fontFamily: "var(--mono)", fontWeight: 500 }}>{r.val}</span>
                  </div>
                ))}
                <div style={{ borderTop: "1px solid var(--border)", paddingTop: 10, marginTop: 4, display: "flex", justifyContent: "space-between" }}>
                  <span style={{ fontSize: 12, fontWeight: 600 }}>Est. Total</span>
                  <span style={{ fontSize: 16, fontFamily: "var(--serif)", fontWeight: 400 }}>
                    ${total.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Footer */}
        {!confirmed && (
          <div style={{ padding: "16px 28px 24px", borderTop: "1px solid var(--border)" }}>
            <button onClick={handleConfirm} disabled={numQty < 1}
              style={{
                width: "100%", padding: "14px 0", borderRadius: 12, border: "none", cursor: numQty > 0 ? "pointer" : "default",
                fontFamily: "var(--sans)", fontSize: 14, fontWeight: 700, letterSpacing: "0.02em",
                background: numQty > 0
                  ? (isBuy ? "var(--gain)" : "var(--loss)")
                  : "#e0dcd5",
                color: numQty > 0 ? "#fff" : "var(--subtle)",
                transition: "all 0.25s ease",
                transform: "scale(1)",
              }}
              onMouseEnter={(e) => { if (numQty > 0) e.currentTarget.style.transform = "scale(1.02)"; }}
              onMouseLeave={(e) => { e.currentTarget.style.transform = "scale(1)"; }}>
              {numQty > 0
                ? `${isBuy ? "Buy" : "Sell"} ${numQty} ${stock.ticker} · $${total.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
                : "Enter quantity to continue"
              }
            </button>
          </div>
        )}
      </div>
    </>
  );
}


/* ═══════════════════════════════════════════════════════════════════
   BRAILLE COMPONENTS (from v2 — ticker tape, spinner, pulse, activity)
   ═══════════════════════════════════════════════════════════════════ */
function BrailleDataStream({ compact = false }) {
  const [offset, setOffset] = useState(0);
  const tapeRef = useRef(null);
  if (!tapeRef.current) {
    const tape = [];
    const brailleChunkSize = compact ? 6 : 14;
    for (let t = 0; t < STREAM_TICKERS.length * 2; t++) {
      for (let b = 0; b < brailleChunkSize; b++) {
        const i = tape.length;
        const v = Math.max(0, Math.min(1, 0.5 + Math.sin(i * 0.18) * 0.3 + Math.sin(i * 0.09 + 1.2) * 0.25 + Math.sin(i * 0.35 + 0.5) * 0.15 + (Math.random() - 0.5) * 0.18));
        tape.push({ type: "braille", value: v });
      }
      tape.push({ type: "ticker", ticker: STREAM_TICKERS[t % STREAM_TICKERS.length] });
      for (let b = 0; b < (compact ? 3 : 5); b++) {
        const i = tape.length;
        tape.push({ type: "braille", value: Math.max(0, Math.min(1, 0.5 + Math.sin(i * 0.2) * 0.3 + (Math.random() - 0.5) * 0.15)) });
      }
    }
    tapeRef.current = tape;
  }
  const tape = tapeRef.current;
  const visibleCount = compact ? 40 : 90;

  useEffect(() => {
    let raf, last = 0;
    const speed = compact ? 100 : 70;
    const step = (ts) => { if (ts - last > speed) { last = ts; setOffset((p) => (p + 1) % tape.length); } raf = requestAnimationFrame(step); };
    raf = requestAnimationFrame(step);
    return () => cancelAnimationFrame(raf);
  }, [tape.length, compact]);

  const elements = [];
  for (let i = 0; i < visibleCount; i++) {
    const idx = (i + offset) % tape.length;
    const cell = tape[idx];
    if (cell.type === "braille") {
      const level = Math.round(cell.value * (B_LEVELS.length - 1));
      let color = cell.value > 0.78 ? "var(--gain)" : cell.value > 0.62 ? "var(--accent)" : "var(--subtle)";
      elements.push(<span key={`b-${i}`} style={{ color, opacity: 0.25 + cell.value * 0.5 }}>{B_LEVELS[level]}</span>);
    } else {
      const { t, p, d, up } = cell.ticker;
      elements.push(
        <span key={`t-${i}`} style={{ display: "inline-flex", alignItems: "center", gap: 4, margin: "0 2px", verticalAlign: "middle" }}>
          <span style={{ fontSize: compact ? 8 : 9.5, fontWeight: 700, color: "var(--text)", letterSpacing: "0.04em", opacity: 0.7 }}>{t}</span>
          <span style={{ fontSize: compact ? 7 : 9, fontFamily: "var(--mono)", fontWeight: 500, color: up ? "var(--gain)" : "var(--loss)", opacity: 0.85 }}>{p}</span>
          <span style={{ fontSize: compact ? 6.5 : 8, fontFamily: "var(--mono)", fontWeight: 600, color: up ? "var(--gain)" : "var(--loss)", opacity: 0.6 }}>{d}%</span>
          <span style={{ color: "var(--border)", opacity: 0.5, margin: "0 1px" }}>│</span>
        </span>
      );
    }
  }
  return (
    <div style={{
      fontFamily: "var(--mono)", fontSize: compact ? 9 : 11, lineHeight: 1.2, letterSpacing: "0.01em",
      overflow: "hidden", whiteSpace: "nowrap", userSelect: "none",
      maskImage: "linear-gradient(90deg, transparent 0%, black 6%, black 94%, transparent 100%)",
      WebkitMaskImage: "linear-gradient(90deg, transparent 0%, black 6%, black 94%, transparent 100%)",
    }}>{elements}</div>
  );
}

function BrailleSpinner() {
  const [f, setF] = useState(0);
  useEffect(() => { const t = setInterval(() => setF((v) => (v + 1) % B_SPIN.length), 80); return () => clearInterval(t); }, []);
  return <span style={{ fontFamily: "var(--mono)", fontSize: 14, color: "var(--gain)", display: "inline-block", width: 14, textAlign: "center" }}>{B_SPIN[f]}</span>;
}

function BraillePulse() {
  const [f, setF] = useState(0);
  useEffect(() => { const t = setInterval(() => setF((v) => (v + 1) % B_HEART.length), 120); return () => clearInterval(t); }, []);
  const chars = Array.from({ length: 18 }, (_, i) => B_HEART[(i + f) % B_HEART.length]);
  return <span style={{ fontFamily: "var(--mono)", fontSize: 11, color: "var(--accent)", opacity: 0.5, userSelect: "none" }}>{chars.join("")}</span>;
}

function BrailleActivity({ intensity = 0.5 }) {
  const [f, setF] = useState(0);
  const seeds = useRef(Array.from({ length: 6 }, () => Math.random() * Math.PI * 2));
  useEffect(() => { const t = setInterval(() => setF((v) => v + 1), 150); return () => clearInterval(t); }, []);
  return (
    <span style={{ fontFamily: "var(--mono)", fontSize: 10, letterSpacing: "-0.03em", userSelect: "none", display: "inline-flex" }}>
      {seeds.current.map((seed, i) => {
        const v = (Math.sin(f * 0.25 + seed + i * 0.8) * 0.5 + 0.5) * intensity;
        return <span key={i} style={{ color: "var(--accent)", opacity: 0.25 + v * 0.65, transition: "opacity 0.2s ease" }}>{B_LEVELS[Math.round(v * (B_LEVELS.length - 1))]}</span>;
      })}
    </span>
  );
}


/* ═══════════════════════════════════════════════════════════════════
   SPARKLINE
   ═══════════════════════════════════════════════════════════════════ */
function Spark({ positive, width = 72, height = 30 }) {
  const pts = useMemo(() => Array.from({ length: 24 }, (_, i) => {
    const t = positive ? i * 0.7 : -i * 0.5;
    return 18 + t + Math.sin(i * 0.6) * 5 + (Math.random() - 0.5) * 3;
  }), [positive]);
  const mn = Math.min(...pts), mx = Math.max(...pts);
  const d = pts.map((p, i) => {
    const x = (i / (pts.length - 1)) * width;
    const y = height - ((p - mn) / (mx - mn || 1)) * height;
    return `${i === 0 ? "M" : "L"}${x.toFixed(1)},${y.toFixed(1)}`;
  }).join(" ");
  const color = positive ? "var(--gain)" : "var(--loss)";
  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`}>
      <defs>
        <linearGradient id={`sp3-${positive}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.25} /><stop offset="100%" stopColor={color} stopOpacity={0} />
        </linearGradient>
      </defs>
      <path d={`${d} L${width},${height} L0,${height} Z`} fill={`url(#sp3-${positive})`} />
      <path d={d} fill="none" stroke={color} strokeWidth="1.8" strokeLinecap="round"
        style={{ strokeDasharray: 200, strokeDashoffset: 200, animation: "drawLine 1.2s ease 0.5s forwards" }} />
    </svg>
  );
}


/* ═══════════════════════════════════════════════════════════════════
   SECTOR ARC RING
   ═══════════════════════════════════════════════════════════════════ */
function SectorRing({ sectors, mounted }) {
  const [hovered, setHovered] = useState(null);
  const cx = 90, cy = 90, radius = 68, strokeW = 22;
  const totalDeg = 340, gapDeg = 3;
  const usable = totalDeg - gapDeg * (sectors.length - 1);
  let segments = [], angle = -170;
  sectors.forEach((s, i) => {
    const sweep = (s.pct / 100) * usable;
    segments.push({ ...s, startAngle: angle, sweep, index: i });
    angle += sweep + gapDeg;
  });
  const arcPath = (startA, sweepA, r) => {
    const s = (startA * Math.PI) / 180, e = ((startA + sweepA) * Math.PI) / 180;
    return `M ${cx + r * Math.cos(s)} ${cy + r * Math.sin(s)} A ${r} ${r} 0 ${sweepA > 180 ? 1 : 0} 1 ${cx + r * Math.cos(e)} ${cy + r * Math.sin(e)}`;
  };
  const circ = 2 * Math.PI * radius;
  const active = hovered !== null ? sectors[hovered] : null;

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 18 }}>
      <svg width={180} height={180} viewBox="0 0 180 180" style={{ overflow: "visible", flexShrink: 0 }}>
        <circle cx={cx} cy={cy} r={radius} fill="none" stroke="#f0ece6" strokeWidth={strokeW} />
        {segments.map((seg) => {
          const isH = hovered === seg.index;
          return (
            <path key={seg.index} d={arcPath(seg.startAngle, seg.sweep, isH ? radius + 4 : radius)}
              fill="none" stroke={seg.color} strokeWidth={isH ? strokeW + 6 : strokeW} strokeLinecap="round"
              style={{
                filter: isH ? `drop-shadow(0 0 8px ${seg.color}44)` : "none",
                transition: "all 0.35s cubic-bezier(0.34, 1.56, 0.64, 1)", cursor: "pointer",
                opacity: hovered !== null && !isH ? 0.35 : 1,
                strokeDasharray: circ, strokeDashoffset: mounted ? 0 : circ,
                animation: mounted ? `arcReveal 1.2s cubic-bezier(0.4, 0, 0.2, 1) ${0.4 + seg.index * 0.12}s both` : "none",
              }}
              onMouseEnter={() => setHovered(seg.index)} onMouseLeave={() => setHovered(null)} />
          );
        })}
        <text x={cx} y={active ? cy - 6 : cy - 4} textAnchor="middle" style={{ fontSize: active ? 22 : 16, fontFamily: "var(--serif)", fill: "var(--text)", transition: "all 0.3s" }}>
          {active ? `${active.pct}%` : "5"}
        </text>
        <text x={cx} y={active ? cy + 12 : cy + 12} textAnchor="middle" style={{ fontSize: 9, fontFamily: "var(--sans)", fill: "var(--muted)", fontWeight: 600, letterSpacing: "0.08em", textTransform: "uppercase" }}>
          {active ? active.name : "SECTORS"}
        </text>
      </svg>
      <div style={{ display: "flex", flexDirection: "column", gap: 10, flex: 1 }}>
        {sectors.map((s, i) => {
          const isH = hovered === i;
          return (
            <div key={s.name} onMouseEnter={() => setHovered(i)} onMouseLeave={() => setHovered(null)}
              style={{
                display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8,
                padding: "5px 10px", borderRadius: 8, cursor: "pointer",
                background: isH ? `${s.color}0D` : "transparent",
                transition: "all 0.25s ease", transform: isH ? "translateX(4px)" : "translateX(0)",
                opacity: hovered !== null && !isH ? 0.4 : 1,
              }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <div style={{ width: 10, height: 10, borderRadius: 3, background: s.color, transition: "transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1)", transform: isH ? "scale(1.4) rotate(45deg)" : "scale(1)" }} />
                <span style={{ fontSize: 12, color: "var(--text)", fontWeight: 500 }}>{s.name}</span>
              </div>
              <span style={{ fontSize: 12, fontFamily: "var(--mono)", fontWeight: 600, color: isH ? s.color : "var(--muted)", transition: "color 0.25s" }}>{s.pct}%</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}


/* ═══════════════════════════════════════════════════════════════════
   VOLUME VIZ
   ═══════════════════════════════════════════════════════════════════ */
function VolumeViz({ data, mounted }) {
  const [hovIdx, setHovIdx] = useState(null);
  const svgRef = useRef(null);

  const w = 400, h = 140;
  const pad = { l: 32, r: 12, t: 16, b: 26 };
  const plotW = w - pad.l - pad.r;
  const plotH = h - pad.t - pad.b;
  const maxV = Math.max(...data.map(d => d.v));
  const barW = plotW / data.length;

  // Market hours
  const mktStartX = (9 / 24) * plotW + pad.l;
  const mktEndX = (17 / 24) * plotW + pad.l;
  const nowHour = 14.5;
  const nowX = (nowHour / 24) * plotW + pad.l;

  // Smooth envelope path through bar tops using catmull-rom → cubic bezier
  const points = data.map((d, i) => ({
    x: pad.l + i * barW + barW / 2,
    y: pad.t + plotH - (d.v / maxV) * plotH,
  }));

  const catmullToBezier = (pts) => {
    if (pts.length < 2) return "";
    let d = `M ${pts[0].x},${pts[0].y}`;
    for (let i = 0; i < pts.length - 1; i++) {
      const p0 = pts[Math.max(i - 1, 0)];
      const p1 = pts[i];
      const p2 = pts[i + 1];
      const p3 = pts[Math.min(i + 2, pts.length - 1)];
      const tension = 6;
      const cp1x = p1.x + (p2.x - p0.x) / tension;
      const cp1y = p1.y + (p2.y - p0.y) / tension;
      const cp2x = p2.x - (p3.x - p1.x) / tension;
      const cp2y = p2.y - (p3.y - p1.y) / tension;
      d += ` C ${cp1x},${cp1y} ${cp2x},${cp2y} ${p2.x},${p2.y}`;
    }
    return d;
  };

  const envelopePath = catmullToBezier(points);
  const areaPath = envelopePath
    + ` L ${points[points.length - 1].x},${pad.t + plotH}`
    + ` L ${points[0].x},${pad.t + plotH} Z`;

  // Horizontal grid lines
  const gridLines = [0.25, 0.5, 0.75].map(pct => pad.t + plotH * (1 - pct));

  // Handle mouse tracking for crosshair
  const onMouseMove = useCallback((e) => {
    if (!svgRef.current) return;
    const rect = svgRef.current.getBoundingClientRect();
    const mouseX = (e.clientX - rect.left) / rect.width * w;
    const idx = Math.round((mouseX - pad.l - barW / 2) / barW);
    if (idx >= 0 && idx < data.length) setHovIdx(idx);
    else setHovIdx(null);
  }, [data.length, barW]);

  const hovData = hovIdx !== null ? data[hovIdx] : null;
  const hovPt = hovIdx !== null ? points[hovIdx] : null;

  return (
    <svg ref={svgRef} width="100%" height={h} viewBox={`0 0 ${w} ${h}`}
      style={{ overflow: "visible", display: "block", cursor: "crosshair" }}
      onMouseMove={onMouseMove} onMouseLeave={() => setHovIdx(null)}>
      <defs>
        <linearGradient id="volArea3" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="var(--accent)" stopOpacity={0.22} />
          <stop offset="60%" stopColor="var(--accent)" stopOpacity={0.06} />
          <stop offset="100%" stopColor="var(--accent)" stopOpacity={0} />
        </linearGradient>
        <linearGradient id="volBar3h" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="var(--accent)" stopOpacity={0.85} />
          <stop offset="100%" stopColor="var(--accent)" stopOpacity={0.35} />
        </linearGradient>
        <linearGradient id="volBar3l" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#c4c0b8" stopOpacity={0.35} />
          <stop offset="100%" stopColor="#c4c0b8" stopOpacity={0.08} />
        </linearGradient>
        <clipPath id="volClip">
          <rect x={pad.l} y={pad.t} width={plotW} height={plotH} />
        </clipPath>
      </defs>

      {/* Grid lines */}
      {gridLines.map((y, i) => (
        <line key={i} x1={pad.l} y1={y} x2={pad.l + plotW} y2={y}
          stroke="var(--border)" strokeWidth={0.5} strokeDasharray="2 4" opacity={0.5} />
      ))}

      {/* Market hours background */}
      <rect x={mktStartX} y={pad.t} width={mktEndX - mktStartX} height={plotH}
        rx={4} fill="var(--accent2)" opacity={0.035} />

      {/* Area fill under envelope */}
      <g clipPath="url(#volClip)">
        <path d={areaPath} fill="url(#volArea3)"
          style={{
            opacity: mounted ? 1 : 0,
            transition: "opacity 1s ease 0.8s",
          }} />
      </g>

      {/* Individual bars */}
      {data.map((d, i) => {
        const barH = Math.max(1, (d.v / maxV) * plotH);
        const x = pad.l + i * barW + barW * 0.15;
        const bw = barW * 0.7;
        const y = pad.t + plotH - barH;
        const isHigh = d.v > 55;
        const isHov = hovIdx === i;

        return (
          <rect key={i} x={x} y={y} width={bw} height={barH}
            rx={bw / 2}
            fill={isHigh ? "url(#volBar3h)" : "url(#volBar3l)"}
            style={{
              opacity: hovIdx !== null ? (isHov ? 1 : 0.3) : (isHigh ? 0.85 : 0.5),
              transition: "opacity 0.2s ease, y 0.3s ease, height 0.3s ease",
              animation: mounted ? `barGrow 0.5s cubic-bezier(0.34, 1.56, 0.64, 1) ${0.6 + i * 0.022}s both` : "none",
              transformOrigin: `${x + bw / 2}px ${pad.t + plotH}px`,
            }}
          />
        );
      })}

      {/* Smooth envelope line */}
      <path d={envelopePath} fill="none" stroke="var(--accent)" strokeWidth={1.5}
        strokeLinecap="round" strokeLinejoin="round" clipPath="url(#volClip)"
        style={{
          opacity: mounted ? 0.5 : 0,
          strokeDasharray: 800,
          strokeDashoffset: mounted ? 0 : 800,
          transition: "stroke-dashoffset 1.8s ease 0.5s, opacity 0.6s ease 0.5s",
        }} />

      {/* "Now" needle */}
      <line x1={nowX} y1={pad.t + 4} x2={nowX} y2={pad.t + plotH}
        stroke="var(--loss)" strokeWidth={1} strokeDasharray="2 3" opacity={0.45} />
      <circle cx={nowX} cy={pad.t + 2} r={2.5} fill="var(--loss)" opacity={0.7}
        style={{ animation: "breathe 2.5s ease infinite" }} />
      <text x={nowX + 8} y={pad.t + 8} style={{ fontSize: 7, fill: "var(--loss)", fontFamily: "var(--mono)", fontWeight: 600, opacity: 0.7 }}>
        NOW
      </text>

      {/* Market hours label */}
      <text x={mktStartX + (mktEndX - mktStartX) / 2} y={pad.t + plotH + 16}
        textAnchor="middle" style={{ fontSize: 7, fill: "var(--accent2)", opacity: 0.35, fontFamily: "var(--mono)", fontWeight: 500, letterSpacing: "0.1em" }}>
        9:30 AM — 4:00 PM
      </text>

      {/* X-axis labels */}
      {[0, 6, 12, 18, 23].map(i => (
        <text key={i} x={pad.l + i * barW + barW / 2} y={h - 4}
          textAnchor="middle"
          style={{ fontSize: 8, fill: "#b0ada6", fontFamily: "var(--mono)", fontWeight: 400 }}>
          {data[i]?.h}
        </text>
      ))}

      {/* Hover crosshair + tooltip */}
      {hovPt && hovData && (
        <g style={{ animation: "fadeIn 0.12s ease" }}>
          {/* Vertical line */}
          <line x1={hovPt.x} y1={pad.t} x2={hovPt.x} y2={pad.t + plotH}
            stroke="var(--text)" strokeWidth={0.5} opacity={0.2} />

          {/* Dot on envelope */}
          <circle cx={hovPt.x} cy={hovPt.y} r={4}
            fill="var(--card)" stroke="var(--accent)" strokeWidth={2} />
          <circle cx={hovPt.x} cy={hovPt.y} r={8}
            fill="var(--accent)" opacity={0.1} />

          {/* Tooltip */}
          <g transform={`translate(${hovPt.x}, ${Math.max(hovPt.y - 40, pad.t)})`}>
            <rect x={-36} y={-10} width={72} height={32} rx={8}
              fill="var(--text)" style={{ filter: "drop-shadow(0 4px 12px rgba(0,0,0,0.12))" }} />
            <polygon points="-4,22 4,22 0,27" fill="var(--text)" />
            <text x={0} y={2} textAnchor="middle"
              style={{ fontSize: 8, fill: "var(--subtle)", fontFamily: "var(--mono)", fontWeight: 400 }}>
              {hovData.h}
            </text>
            <text x={0} y={14} textAnchor="middle"
              style={{ fontSize: 11, fill: "#fff", fontFamily: "var(--mono)", fontWeight: 600 }}>
              {hovData.v}M
            </text>
          </g>
        </g>
      )}
    </svg>
  );
}


/* ═══════════════════════════════════════════════════════════════════
   CHART TOOLTIP
   ═══════════════════════════════════════════════════════════════════ */
function ChartTip({ active, payload, label }) {
  if (!active || !payload?.length) return null;
  return (
    <div style={{ background: "var(--card)", border: "1px solid var(--border)", borderRadius: 10, padding: "10px 14px", boxShadow: "0 8px 32px rgba(0,0,0,0.08)" }}>
      <div style={{ fontSize: 10, color: "var(--muted)", marginBottom: 3, fontFamily: "var(--sans)" }}>{label}</div>
      <div style={{ fontSize: 16, fontWeight: 600, color: "var(--text)", fontFamily: "var(--serif)" }}>${payload[0].value?.toLocaleString()}</div>
    </div>
  );
}


/* ═══════════════════════════════════════════════════════════════════
   MAIN DASHBOARD
   ═══════════════════════════════════════════════════════════════════ */
export default function Dashboard() {
  const [tf, setTf] = useState("3M");
  const [expanded, setExpanded] = useState(null);
  const [search, setSearch] = useState("");
  const [mounted, setMounted] = useState(false);
  const [now, setNow] = useState(new Date());
  const [hoveredHolding, setHoveredHolding] = useState(null);
  const [chartTicker, setChartTicker] = useState(null); // null = portfolio
  const [tradeStock, setTradeStock] = useState(null); // null = closed
  const chartCardRef = useRef(null);
  const mouse = useMouse(chartCardRef);
  const ripple = useRipple();

  useEffect(() => { setMounted(true); const t = setInterval(() => setNow(new Date()), 1000); return () => clearInterval(t); }, []);

  const totalVal = HOLDINGS.reduce((s, h) => s + h.shares * h.current, 0);
  const totalCost = HOLDINGS.reduce((s, h) => s + h.shares * h.avg, 0);
  const totalGain = totalVal - totalCost;
  const totalPct = ((totalGain / totalCost) * 100);

  const gainSpring = useSpring(mounted ? totalGain : 0, 0.04, 0.85);
  const pctSpring = useSpring(mounted ? totalPct : 0, 0.04, 0.85);

  const filtered = WATCHLIST.filter(s => !search || s.ticker.toLowerCase().includes(search.toLowerCase()) || s.name.toLowerCase().includes(search.toLowerCase()));

  // Chart data — portfolio or individual stock
  const chartData = chartTicker ? STOCK_HISTORIES[chartTicker] : HISTORY;
  const chartStock = chartTicker ? WATCHLIST.find((s) => s.ticker === chartTicker) : null;
  const chartColor = chartStock ? (chartStock.pct >= 0 ? "#2d6a4f" : "#b5342b") : "#2d6a4f";
  const chartLabel = chartTicker ? chartTicker : "Portfolio";
  const chartYFormat = chartTicker
    ? (v) => `$${v.toFixed(0)}`
    : (v) => `$${(v / 1000).toFixed(0)}k`;

  // Magnetic tilt for chart card
  const chartTilt = mouse.active
    ? { transform: `perspective(800px) rotateY(${(mouse.x - 0.5) * 5}deg) rotateX(${-(mouse.y - 0.5) * 5}deg)`, transition: "transform 0.1s ease" }
    : { transform: "perspective(800px) rotateY(0) rotateX(0)", transition: "transform 0.5s ease" };

  return (
    <>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Instrument+Serif:ital@0;1&family=Outfit:wght@300;400;500;600;700&family=IBM+Plex+Mono:wght@400;500&display=swap');
        :root {
          --bg: #f6f4f0; --card: #ffffff; --text: #1a1a2e; --muted: #8a8a8a; --subtle: #c4c0b8;
          --border: #e8e4de; --gain: #2d6a4f; --loss: #b5342b; --accent: #c9a96e; --accent2: #2d6a4f;
          --serif: 'Instrument Serif', Georgia, serif; --sans: 'Outfit', system-ui, sans-serif; --mono: 'IBM Plex Mono', monospace;
        }
        * { margin: 0; padding: 0; box-sizing: border-box; }

        @keyframes fadeUp { from { opacity: 0; transform: translateY(24px) scale(0.98); } to { opacity: 1; transform: translateY(0) scale(1); } }
        @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
        @keyframes drawLine { to { stroke-dashoffset: 0; } }
        @keyframes slideWidth { from { width: 0; } }
        @keyframes breathe { 0%, 100% { opacity: 1; transform: scale(1); } 50% { opacity: 0.5; transform: scale(1.3); } }
        @keyframes arcReveal { from { stroke-dashoffset: 430; } to { stroke-dashoffset: 0; } }
        @keyframes barGrow { from { transform: scaleY(0); opacity: 0; } to { transform: scaleY(1); opacity: 1; } }

        @keyframes orbFloat {
          0% { transform: translate(0, 0) scale(1); }
          25% { transform: translate(30px, -20px) scale(1.05); }
          50% { transform: translate(-20px, 15px) scale(0.95); }
          75% { transform: translate(15px, 25px) scale(1.02); }
          100% { transform: translate(0, 0) scale(1); }
        }
        @keyframes rippleExpand {
          to { transform: translate(-50%, -50%) scale(1); opacity: 0; }
        }
        @keyframes odoFadeIn {
          from { opacity: 0; transform: translateY(8px); }
          to { opacity: 1; transform: translateY(0); }
        }
        @keyframes cardEntrance {
          0% { opacity: 0; transform: translateY(32px) scale(0.94) rotateX(4deg); }
          60% { transform: translateY(-4px) scale(1.01) rotateX(-1deg); }
          100% { opacity: 1; transform: translateY(0) scale(1) rotateX(0); }
        }
        @keyframes glowPulse {
          0%, 100% { box-shadow: 0 0 0 0 rgba(45,106,79,0); }
          50% { box-shadow: 0 0 0 6px rgba(45,106,79,0.06); }
        }
        @keyframes drawerSlideIn {
          from { transform: translateX(100%); }
          to { transform: translateX(0); }
        }
        @keyframes drawerSlideOut {
          from { transform: translateX(0); }
          to { transform: translateX(100%); }
        }
        @keyframes fadeOut {
          from { opacity: 1; }
          to { opacity: 0; }
        }

        .card3 {
          background: var(--card); border: 1px solid var(--border); border-radius: 16px;
          position: relative; overflow: hidden;
          animation: cardEntrance 0.7s cubic-bezier(0.34, 1.56, 0.64, 1) both;
          transition: box-shadow 0.3s ease, border-color 0.3s ease;
        }
        .card3:hover {
          box-shadow: 0 12px 40px rgba(0,0,0,0.06);
          border-color: #ddd8d0;
        }
        .card3::before {
          content: ''; position: absolute; inset: 0;
          background: url("data:image/svg+xml,%3Csvg viewBox='0 0 256 256' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)' opacity='0.025'/%3E%3C/svg%3E");
          pointer-events: none; z-index: 0;
        }
        .card3 > * { position: relative; z-index: 1; }

        .watch-row3 { transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1); cursor: pointer; }
        .watch-row3:hover { background: #faf8f5 !important; transform: translateX(3px); }

        .holding3 { transition: all 0.35s cubic-bezier(0.4, 0, 0.2, 1); cursor: pointer; }
        .holding3:hover { border-color: var(--accent) !important; box-shadow: 0 8px 32px rgba(201,169,110,0.12); transform: translateY(-2px); }

        .tf-btn3 { transition: all 0.25s ease; cursor: pointer; position: relative; }
        .tf-btn3::after { content: ''; position: absolute; bottom: -2px; left: 50%; transform: translateX(-50%); width: 0; height: 2px; background: var(--accent2); border-radius: 1px; transition: width 0.25s ease; }
        .tf-btn3.active::after { width: 100%; }

        ::-webkit-scrollbar { width: 5px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb { background: var(--border); border-radius: 3px; }
      `}</style>

      <div style={{ minHeight: "100vh", background: "var(--bg)", fontFamily: "var(--sans)", color: "var(--text)", position: "relative" }}>
        <AmbientOrbs />

        {/* Grain */}
        <div style={{
          position: "fixed", inset: 0, pointerEvents: "none", zIndex: 999, opacity: 0.28,
          background: `url("data:image/svg+xml,%3Csvg viewBox='0 0 512 512' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)' opacity='0.08'/%3E%3C/svg%3E")`,
        }} />

        <div style={{ maxWidth: 1400, margin: "0 auto", padding: "20px 32px", position: "relative", zIndex: 1 }}>

          {/* ── HEADER ── */}
          <header style={{
            display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 0,
            animation: mounted ? "fadeIn 0.6s ease forwards" : "none",
          }}>
            <div style={{ display: "flex", alignItems: "baseline", gap: 12 }}>
              <span style={{ fontFamily: "var(--serif)", fontSize: 26, fontStyle: "italic", letterSpacing: "-0.02em" }}>Meridian</span>
              <span style={{ fontSize: 10, color: "var(--subtle)", fontWeight: 500, letterSpacing: "0.12em", textTransform: "uppercase" }}>Capital Terminal</span>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: 20 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <BrailleSpinner />
                <span style={{ fontSize: 11, color: "var(--muted)", fontWeight: 500 }}>NYSE Open</span>
              </div>
              <div style={{ height: 14, width: 1, background: "var(--border)" }} />
              <span style={{ fontSize: 12, fontFamily: "var(--mono)", color: "var(--muted)" }}>
                {now.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" })} · {now.toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit", second: "2-digit" })}
              </span>
            </div>
          </header>

          {/* ── BRAILLE TICKER TAPE ── */}
          <div style={{
            margin: "14px 0 22px", padding: "8px 0",
            borderTop: "1px solid var(--border)", borderBottom: "1px solid var(--border)",
            animation: mounted ? "fadeIn 0.8s ease 0.3s both" : "none",
          }}>
            <BrailleDataStream />
          </div>

          {/* ── PORTFOLIO HERO ── */}
          <section style={{ marginBottom: 24, animation: mounted ? "fadeUp 0.7s ease 0.1s both" : "none" }}>
            <div style={{ display: "flex", alignItems: "flex-end", justifyContent: "space-between", gap: 32 }}>
              <div>
                <div style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.14em", marginBottom: 8 }}>
                  Total Portfolio Value
                </div>
                <Odometer value={totalVal} fontSize={62} />
                <div style={{ display: "flex", alignItems: "center", gap: 14, marginTop: 12 }}>
                  <div style={{ display: "inline-flex", alignItems: "center", gap: 6, background: "rgba(45,106,79,0.08)", padding: "6px 14px", borderRadius: 100, animation: mounted ? "glowPulse 3s ease 2s infinite" : "none" }}>
                    <svg width="10" height="10" viewBox="0 0 10 10"><path d="M5 1L9 6H1L5 1Z" fill="var(--gain)" /></svg>
                    <span style={{ fontSize: 14, fontFamily: "var(--mono)", fontWeight: 500, color: "var(--gain)" }}>
                      +${gainSpring.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </span>
                    <span style={{ fontSize: 13, fontFamily: "var(--mono)", fontWeight: 500, color: "var(--gain)", opacity: 0.7 }}>
                      ({pctSpring.toFixed(2)}%)
                    </span>
                  </div>
                  <span style={{ fontSize: 11, color: "var(--subtle)", fontWeight: 500 }}>all time unrealized</span>
                </div>
              </div>
              <div style={{ display: "flex", gap: 6, flexShrink: 0 }}>
                {[
                  { n: "S&P 500", v: "5,892", c: "+0.87", up: true },
                  { n: "NASDAQ", v: "18,742", c: "+1.12", up: true },
                  { n: "DOW 30", v: "43,218", c: "+0.34", up: true },
                ].map((idx, i) => (
                  <div key={idx.n} style={{
                    padding: "10px 16px", borderRadius: 12,
                    border: "1px solid var(--border)", background: "var(--card)", minWidth: 120,
                    animation: mounted ? `cardEntrance 0.6s cubic-bezier(0.34, 1.56, 0.64, 1) ${0.3 + i * 0.08}s both` : "none",
                  }}>
                    <div style={{ fontSize: 10, color: "var(--subtle)", fontWeight: 600, letterSpacing: "0.06em", marginBottom: 4 }}>{idx.n}</div>
                    <div style={{ fontFamily: "var(--mono)", fontSize: 14, fontWeight: 500 }}>{idx.v}</div>
                    <div style={{ fontFamily: "var(--mono)", fontSize: 11, fontWeight: 500, color: idx.up ? "var(--gain)" : "var(--loss)", marginTop: 2 }}>{idx.c}%</div>
                  </div>
                ))}
              </div>
            </div>
          </section>

          {/* ── MAIN GRID ── */}
          <div style={{ display: "grid", gridTemplateColumns: "1fr 360px", gap: 20 }}>
            <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>

              {/* Chart */}
              <div ref={chartCardRef} className="card3" onClick={ripple.trigger}
                style={{ ...chartTilt, padding: "24px 28px 16px", animationDelay: "0.15s", transformStyle: "preserve-3d" }}>
                <ripple.RippleLayer />
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                    {chartTicker && (
                      <button onClick={(e) => { e.stopPropagation(); setChartTicker(null); }} style={{
                        width: 28, height: 28, borderRadius: 8, border: "1px solid var(--border)",
                        background: "transparent", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
                        transition: "all 0.2s",
                      }}
                      onMouseEnter={(e) => { e.currentTarget.style.background = "#f8f6f2"; }}
                      onMouseLeave={(e) => { e.currentTarget.style.background = "transparent"; }}>
                        <svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="var(--muted)" strokeWidth="1.5" strokeLinecap="round">
                          <path d="M8 1L3 6L8 11" />
                        </svg>
                      </button>
                    )}
                    <div>
                      <div style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.12em" }}>
                        {chartTicker ? `${chartStock?.name}` : "Performance"}
                      </div>
                      {chartTicker && chartStock && (
                        <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 3 }}>
                          <span style={{ fontFamily: "var(--mono)", fontSize: 18, fontWeight: 600 }}>${chartStock.price.toFixed(2)}</span>
                          <span style={{ fontFamily: "var(--mono)", fontSize: 12, fontWeight: 500, color: chartStock.pct >= 0 ? "var(--gain)" : "var(--loss)" }}>
                            {chartStock.pct >= 0 ? "+" : ""}{chartStock.pct}%
                          </span>
                          <button onClick={(e) => { e.stopPropagation(); setTradeStock(chartStock); }} style={{
                            marginLeft: 4, padding: "3px 12px", borderRadius: 8, border: "1px solid var(--accent)",
                            background: "rgba(201,169,110,0.06)", cursor: "pointer",
                            fontFamily: "var(--sans)", fontSize: 10, fontWeight: 700, color: "var(--accent)",
                            letterSpacing: "0.06em", textTransform: "uppercase", transition: "all 0.2s",
                          }}
                          onMouseEnter={(e) => { e.currentTarget.style.background = "rgba(201,169,110,0.14)"; }}
                          onMouseLeave={(e) => { e.currentTarget.style.background = "rgba(201,169,110,0.06)"; }}>
                            Trade
                          </button>
                        </div>
                      )}
                    </div>
                  </div>
                  <div style={{ display: "flex", gap: 2 }}>
                    {TF.map((t) => (
                      <button key={t} className={`tf-btn3 ${tf === t ? "active" : ""}`} onClick={(e) => { e.stopPropagation(); setTf(t); }}
                        style={{
                          padding: "4px 12px", border: "none", borderRadius: 6, fontSize: 11, fontWeight: 600, fontFamily: "var(--sans)", letterSpacing: "0.02em",
                          background: tf === t ? `${chartColor}14` : "transparent", color: tf === t ? chartColor : "var(--subtle)",
                        }}>{t}</button>
                    ))}
                  </div>
                </div>
                <div style={{ height: chartTicker ? 210 : 240, marginLeft: -8, transition: "height 0.3s ease" }}>
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={chartData} key={chartLabel}>
                      <defs>
                        <linearGradient id="cg3" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="0%" stopColor={chartColor} stopOpacity={0.12} /><stop offset="50%" stopColor={chartColor} stopOpacity={0.04} /><stop offset="100%" stopColor={chartColor} stopOpacity={0} />
                        </linearGradient>
                      </defs>
                      <XAxis dataKey="date" tick={{ fontSize: 10, fill: "#b0ada6", fontFamily: "var(--mono)" }} axisLine={false} tickLine={false} interval={14} />
                      <YAxis tick={{ fontSize: 10, fill: "#b0ada6", fontFamily: "var(--mono)" }} axisLine={false} tickLine={false} tickFormatter={chartYFormat} width={48} domain={["auto", "auto"]} />
                      <Tooltip content={<ChartTip />} cursor={{ stroke: "var(--border)", strokeDasharray: "4 4" }} />
                      <Area type="monotone" dataKey="value" stroke={chartColor} strokeWidth={2.5} fill="url(#cg3)" dot={false} animationDuration={800} animationEasing="ease-out" />
                    </AreaChart>
                  </ResponsiveContainer>
                </div>
              </div>

              {/* Holdings + Allocation Row */}
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20 }}>
                <div className="card3" style={{ padding: "22px 24px", animationDelay: "0.25s" }}>
                  <div style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.12em", marginBottom: 16 }}>Holdings</div>
                  <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                    {HOLDINGS.map((h, idx) => {
                      const gain = (h.current - h.avg) / h.avg * 100;
                      const pos = gain >= 0;
                      const mktVal = h.shares * h.current;
                      const weight = (mktVal / totalVal * 100);
                      const isSelected = chartTicker === h.ticker;
                      return (
                        <div key={h.ticker} className="holding3"
                          onClick={() => setChartTicker(isSelected ? null : h.ticker)}
                          style={{
                          padding: "12px 14px", borderRadius: 12,
                          border: `1px solid ${isSelected ? "var(--accent2)" : hoveredHolding === h.ticker ? "var(--accent)" : "var(--border)"}`,
                          background: isSelected ? "rgba(45,106,79,0.04)" : hoveredHolding === h.ticker ? "#fdfcfa" : "transparent",
                        }}
                        onMouseEnter={() => setHoveredHolding(h.ticker)} onMouseLeave={() => setHoveredHolding(null)}>
                          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 8 }}>
                            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                              {isSelected && (
                                <div style={{
                                  width: 6, height: 6, borderRadius: "50%", background: "var(--accent2)", flexShrink: 0,
                                  boxShadow: "0 0 0 3px rgba(45,106,79,0.12)",
                                  animation: "fadeIn 0.2s ease",
                                }} />
                              )}
                              <div>
                                <span style={{ fontSize: 14, fontWeight: 600 }}>{h.ticker}</span>
                                <span style={{ fontSize: 10, color: "var(--subtle)", marginLeft: 8 }}>{h.shares} × ${h.avg}</span>
                              </div>
                            </div>
                            <div style={{ textAlign: "right" }}>
                              <span style={{ fontSize: 13, fontFamily: "var(--mono)", fontWeight: 500 }}>
                                  ${mktVal.toLocaleString("en-US", { maximumFractionDigits: 0 })}
                                </span>
                              <div style={{ fontSize: 11, fontFamily: "var(--mono)", fontWeight: 500, color: pos ? "var(--gain)" : "var(--loss)" }}>
                                {pos ? "+" : ""}{gain.toFixed(1)}%
                              </div>
                            </div>
                          </div>
                          <div style={{ height: 3, borderRadius: 2, background: "var(--border)", overflow: "hidden" }}>
                            <div style={{ height: "100%", borderRadius: 2, background: pos ? "var(--gain)" : "var(--loss)", width: `${weight}%`, opacity: 0.6, animation: `slideWidth 1s ease ${0.5 + idx * 0.08}s both` }} />
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
                  <div className="card3" style={{ padding: "22px 24px", flex: 1, animationDelay: "0.3s" }}>
                    <div style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.12em", marginBottom: 14 }}>Sector Allocation</div>
                    <SectorRing sectors={SECTORS} mounted={mounted} />
                  </div>
                  <div className="card3" style={{ padding: "22px 24px", flex: 1, animationDelay: "0.35s" }}>
                    <div style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.12em", marginBottom: 8 }}>Volume Profile</div>
                    <VolumeViz data={VOLUME} mounted={mounted} />
                  </div>
                </div>
              </div>
            </div>

            {/* ── RIGHT SIDEBAR ── */}
            <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
              <div style={{
                display: "flex", alignItems: "center", gap: 10,
                padding: "10px 16px", borderRadius: 12, border: "1px solid var(--border)", background: "var(--card)",
                animation: mounted ? "cardEntrance 0.5s cubic-bezier(0.34, 1.56, 0.64, 1) 0.2s both" : "none",
                transition: "border-color 0.3s, box-shadow 0.3s",
              }}
              onFocus={(e) => { e.currentTarget.style.borderColor = "var(--accent)"; e.currentTarget.style.boxShadow = "0 0 0 3px rgba(201,169,110,0.1)"; }}
              onBlur={(e) => { e.currentTarget.style.borderColor = "var(--border)"; e.currentTarget.style.boxShadow = "none"; }}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="var(--subtle)" strokeWidth="2" strokeLinecap="round">
                  <circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" />
                </svg>
                <input type="text" placeholder="Search ticker or name…" value={search} onChange={(e) => setSearch(e.target.value)}
                  style={{ flex: 1, border: "none", outline: "none", background: "none", fontFamily: "var(--sans)", fontSize: 13, color: "var(--text)" }} />
                {search && <button onClick={() => setSearch("")} style={{ background: "none", border: "none", cursor: "pointer", color: "var(--subtle)", fontSize: 16, lineHeight: 1, transition: "transform 0.2s", }} onMouseEnter={e => e.currentTarget.style.transform = "rotate(90deg)"} onMouseLeave={e => e.currentTarget.style.transform = "rotate(0)"}>×</button>}
              </div>

              <div className="card3" style={{ flex: 1, overflow: "hidden", animationDelay: "0.22s" }}>
                <div style={{ padding: "20px 22px 12px" }}>
                  <div style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.12em" }}>Watchlist</div>
                </div>
                <div style={{ overflow: "auto", maxHeight: 440 }}>
                  {filtered.map((s, i) => {
                    const pos = s.change >= 0;
                    const isExp = expanded === s.ticker;
                    return (
                      <div key={s.ticker}>
                        <div className="watch-row3" onClick={() => setExpanded(isExp ? null : s.ticker)}
                          style={{
                            display: "flex", alignItems: "center", justifyContent: "space-between",
                            padding: "14px 22px", gap: 8, borderBottom: "1px solid rgba(0,0,0,0.04)",
                            background: isExp ? "#faf8f5" : "transparent",
                          }}>
                          <div style={{ minWidth: 0, flex: 1 }}>
                            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                              <span style={{ fontSize: 14, fontWeight: 600, letterSpacing: "-0.01em" }}>{s.ticker}</span>
                              <BrailleActivity intensity={Math.min(1, Math.abs(s.pct) / 2 + 0.2)} />
                              <svg width="8" height="8" viewBox="0 0 8 8" style={{ transition: "transform 0.3s ease", transform: isExp ? "rotate(180deg)" : "rotate(0deg)" }}>
                                <path d="M1 3L4 6L7 3" stroke="var(--subtle)" strokeWidth="1.5" fill="none" strokeLinecap="round" />
                              </svg>
                            </div>
                            <div style={{ fontSize: 11, color: "var(--subtle)", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{s.name}</div>
                          </div>
                          <Spark positive={pos} />
                          <div style={{ textAlign: "right", flexShrink: 0, minWidth: 72 }}>
                            <span style={{ fontSize: 14, fontFamily: "var(--mono)", fontWeight: 500, letterSpacing: "-0.02em" }}>${s.price.toFixed(2)}</span>
                            <div style={{ fontSize: 11, fontFamily: "var(--mono)", fontWeight: 500, color: pos ? "var(--gain)" : "var(--loss)" }}>
                              {pos ? "+" : ""}{s.pct.toFixed(2)}%
                            </div>
                          </div>
                        </div>
                        <div style={{
                          maxHeight: isExp ? 180 : 0, overflow: "hidden",
                          transition: "max-height 0.4s cubic-bezier(0.4, 0, 0.2, 1), opacity 0.3s ease",
                          opacity: isExp ? 1 : 0, background: "#f8f6f2",
                          borderBottom: isExp ? "1px solid var(--border)" : "none",
                        }}>
                          <div style={{ padding: "14px 22px", display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr", gap: 8 }}>
                            {[{ label: "Open", val: `$${s.open}` }, { label: "High", val: `$${s.high}` }, { label: "Low", val: `$${s.low}` }, { label: "Volume", val: s.vol }].map(d => (
                              <div key={d.label}>
                                <div style={{ fontSize: 9, color: "var(--subtle)", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.1em", marginBottom: 3 }}>{d.label}</div>
                                <div style={{ fontSize: 13, fontFamily: "var(--mono)", fontWeight: 500 }}>{d.val}</div>
                              </div>
                            ))}
                          </div>
                          <div style={{ padding: "0 22px 14px" }}>
                            <div style={{ display: "flex", justifyContent: "space-between", fontSize: 10, color: "var(--subtle)", marginBottom: 4 }}>
                              <span>Day Range</span><span>${s.low} — ${s.high}</span>
                            </div>
                            <div style={{ height: 4, borderRadius: 2, background: "#e8e4de", position: "relative" }}>
                              <div style={{
                                position: "absolute", top: -2, width: 8, height: 8, borderRadius: "50%",
                                background: pos ? "var(--gain)" : "var(--loss)", border: "2px solid var(--card)",
                                boxShadow: `0 1px 4px rgba(0,0,0,0.15), 0 0 0 3px ${pos ? "rgba(45,106,79,0.15)" : "rgba(181,52,43,0.15)"}`,
                                left: `${((s.price - s.low) / (s.high - s.low)) * 100}%`, transform: "translateX(-50%)",
                                transition: "left 0.6s cubic-bezier(0.34, 1.56, 0.64, 1)",
                              }} />
                            </div>
                          </div>
                          {/* Action buttons */}
                          <div style={{ padding: "0 22px 14px", display: "flex", gap: 8 }}>
                            <button onClick={(e) => { e.stopPropagation(); setChartTicker(s.ticker); }}
                              style={{
                                flex: 1, padding: "8px 0", borderRadius: 8,
                                border: "1px solid var(--border)", background: "var(--card)",
                                fontFamily: "var(--sans)", fontSize: 11, fontWeight: 600, color: "var(--text)",
                                cursor: "pointer", transition: "all 0.2s", display: "flex", alignItems: "center", justifyContent: "center", gap: 6,
                              }}
                              onMouseEnter={(e) => { e.currentTarget.style.borderColor = "var(--accent2)"; e.currentTarget.style.background = "rgba(45,106,79,0.04)"; }}
                              onMouseLeave={(e) => { e.currentTarget.style.borderColor = "var(--border)"; e.currentTarget.style.background = "var(--card)"; }}>
                              <svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round">
                                <polyline points="1 8 4 4 7 6 11 2" />
                              </svg>
                              View Chart
                            </button>
                            <button onClick={(e) => { e.stopPropagation(); setTradeStock(s); }}
                              style={{
                                flex: 1, padding: "8px 0", borderRadius: 8,
                                border: "1px solid var(--accent)", background: "rgba(201,169,110,0.06)",
                                fontFamily: "var(--sans)", fontSize: 11, fontWeight: 700, color: "var(--accent)",
                                cursor: "pointer", transition: "all 0.2s", letterSpacing: "0.03em",
                              }}
                              onMouseEnter={(e) => { e.currentTarget.style.background = "rgba(201,169,110,0.14)"; }}
                              onMouseLeave={(e) => { e.currentTarget.style.background = "rgba(201,169,110,0.06)"; }}>
                              Trade
                            </button>
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>

              <div className="card3" style={{ padding: "20px 22px 16px", animationDelay: "0.4s" }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.12em", marginBottom: 14, display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                  <span>Market Pulse</span>
                  <BraillePulse />
                </div>
                <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
                  {NEWS.map((n, i) => (
                    <div key={i} style={{
                      display: "flex", gap: 12, alignItems: "flex-start",
                      animation: mounted ? `fadeUp 0.4s ease ${0.5 + i * 0.06}s both` : "none",
                      transition: "transform 0.2s ease",
                    }}
                    onMouseEnter={(e) => e.currentTarget.style.transform = "translateX(4px)"}
                    onMouseLeave={(e) => e.currentTarget.style.transform = "translateX(0)"}>
                      <span style={{
                        fontSize: 9, fontWeight: 600, padding: "3px 8px", borderRadius: 6,
                        background: n.tag === "Earnings" ? "rgba(45,106,79,0.08)" : n.tag === "Bonds" ? "rgba(181,52,43,0.08)" : "rgba(201,169,110,0.1)",
                        color: n.tag === "Earnings" ? "var(--gain)" : n.tag === "Bonds" ? "var(--loss)" : "var(--accent)",
                        whiteSpace: "nowrap", flexShrink: 0, marginTop: 2, letterSpacing: "0.04em", textTransform: "uppercase",
                        transition: "transform 0.2s ease",
                      }}>{n.tag}</span>
                      <div>
                        <div style={{ fontSize: 12, color: "var(--text)", lineHeight: 1.45 }}>{n.text}</div>
                        <div style={{ fontSize: 10, color: "var(--subtle)", marginTop: 3, fontFamily: "var(--mono)" }}>{n.time} ago</div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>

          {/* ── FOOTER ── */}
          <footer style={{
            marginTop: 24, padding: "16px 0", borderTop: "1px solid var(--border)",
            display: "flex", justifyContent: "space-between", alignItems: "center",
            animation: mounted ? "fadeIn 0.8s ease 0.6s both" : "none",
          }}>
            <span style={{ fontSize: 10, color: "var(--subtle)" }}>Simulated data for demonstration. Not financial advice.</span>
            <div style={{ flex: 1, maxWidth: 300, margin: "0 24px", opacity: 0.4, overflow: "hidden" }}>
              <BrailleDataStream compact />
            </div>
            <span style={{ fontSize: 10, color: "var(--subtle)", fontFamily: "var(--mono)" }}>Meridian Terminal v4.0</span>
          </footer>
        </div>
      </div>

      {/* Trade Drawer */}
      {tradeStock && <TradeDrawer stock={tradeStock} onClose={() => setTradeStock(null)} />}
    </>
  );
}
