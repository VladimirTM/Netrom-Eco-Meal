import { useState } from "react";

interface ReadOnlyStarRatingProps {
  value: number;
  size?: string;
  showValue?: boolean;
  count?: number;
  editable?: false;
}

interface EditableStarRatingProps {
  editable: true;
  selectedValue: number;
  onSelectedValueChange: (value: number) => void;
  size?: string;
}

type StarRatingProps = ReadOnlyStarRatingProps | EditableStarRatingProps;

// Ports StarRating.razor exactly: read-only mode is a fractional gold fill over a grey track,
// editable mode is a 1-5 click/hover picker.
function StarRating(props: StarRatingProps) {
  const [hovered, setHovered] = useState(0);

  if (props.editable) {
    const { selectedValue, onSelectedValueChange, size = "1rem" } = props;
    return (
      <div className="star-rating-input" style={{ fontSize: size }} role="radiogroup" aria-label="Your rating">
        {[1, 2, 3, 4, 5].map((starValue) => (
          <button
            key={starValue}
            type="button"
            className="star-rating-btn"
            aria-label={`${starValue} star${starValue === 1 ? "" : "s"}`}
            onMouseEnter={() => setHovered(starValue)}
            onMouseLeave={() => setHovered(0)}
            onClick={() => onSelectedValueChange(starValue)}
          >
            <i className={`bi ${starValue <= (hovered > 0 ? hovered : selectedValue) ? "bi-star-fill" : "bi-star"}`} />
          </button>
        ))}
      </div>
    );
  }

  const { value, size = "1rem", showValue, count } = props;
  const fillPercent = (Math.min(Math.max(value, 0), 5) / 5) * 100;

  return (
    <span className="star-rating-group">
      <span className="star-rating" style={{ fontSize: size }} aria-label={`${value.toFixed(1)} out of 5 stars`}>
        <span className="star-rating-track">
          {Array.from({ length: 5 }, (_, i) => (
            <i className="bi bi-star-fill" key={i} />
          ))}
        </span>
        <span className="star-rating-fill" style={{ width: `${fillPercent}%` }}>
          {Array.from({ length: 5 }, (_, i) => (
            <i className="bi bi-star-fill" key={i} />
          ))}
        </span>
      </span>
      {showValue && (
        <span className="star-rating-value">
          {value.toFixed(1)}
          {count !== undefined ? ` (${count})` : ""}
        </span>
      )}
    </span>
  );
}

export default StarRating;
