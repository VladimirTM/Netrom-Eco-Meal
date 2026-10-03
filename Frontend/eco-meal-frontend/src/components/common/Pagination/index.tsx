interface PaginationProps {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

function Pagination({ currentPage, totalPages, onPageChange }: PaginationProps) {
  if (totalPages <= 1) return null;

  return (
    <nav className="d-flex justify-content-between align-items-center px-4 py-3 border-top">
      <span className="text-muted small">
        Page {currentPage} of {totalPages}
      </span>
      <div className="btn-group">
        <button
          type="button"
          className="btn btn-sm btn-outline-secondary"
          disabled={currentPage <= 1}
          onClick={() => onPageChange(currentPage - 1)}
        >
          <i className="bi bi-chevron-left" /> Prev
        </button>
        <button
          type="button"
          className="btn btn-sm btn-outline-secondary"
          disabled={currentPage >= totalPages}
          onClick={() => onPageChange(currentPage + 1)}
        >
          Next <i className="bi bi-chevron-right" />
        </button>
      </div>
    </nav>
  );
}

export default Pagination;
