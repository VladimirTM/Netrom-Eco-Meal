// Loading placeholders built from Bootstrap's .placeholder/.placeholder-glow utilities rather
// than a new custom style.
interface SkeletonCardsProps {
  count?: number;
}

export function BusinessCardSkeletons({ count = 6 }: SkeletonCardsProps) {
  return (
    <div className="biz-grid">
      {Array.from({ length: count }, (_, i) => (
        <div className="biz-card placeholder-glow" key={i} aria-hidden="true">
          <div className="biz-card-media" style={{ backgroundColor: "var(--em-border, #ddd)" }} />
          <div className="biz-card-body">
            <span className="placeholder col-4 mb-2 d-block" />
            <span className="placeholder col-8 mb-2 d-block" />
            <span className="placeholder col-6 d-block" />
          </div>
        </div>
      ))}
    </div>
  );
}

export function ListRowSkeletons({ count = 3 }: SkeletonCardsProps) {
  return (
    <div className="placeholder-glow" aria-hidden="true">
      {Array.from({ length: count }, (_, i) => (
        <p className="mb-2" key={i}>
          <span className="placeholder col-7" /> <span className="placeholder col-3" />
        </p>
      ))}
    </div>
  );
}
