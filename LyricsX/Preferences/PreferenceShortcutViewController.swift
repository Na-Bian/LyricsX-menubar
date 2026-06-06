import AppKit
import GenericID
import MASShortcut

class PreferenceShortcutViewController: PreferenceViewController {
    override func viewDidLoad() {
        super.viewDidLoad()

        guard let gridView = view.firstSubview(ofType: NSGridView.self) else {
            return
        }

        // Rows 1 and 2 belong to karaoke lyrics and lyrics window shortcuts.
        for rowIndex in [2, 1] where gridView.numberOfRows > rowIndex {
            let row = gridView.row(at: rowIndex)
            for columnIndex in 0..<gridView.numberOfColumns {
                row.cell(at: columnIndex).contentView?.removeFromSuperview()
            }
            gridView.removeRow(at: rowIndex)
        }
        gridView.needsLayout = true
    }
}

private extension NSView {
    func firstSubview<T: NSView>(ofType type: T.Type) -> T? {
        if let view = self as? T {
            return view
        }
        for subview in subviews {
            if let match = subview.firstSubview(ofType: type) {
                return match
            }
        }
        return nil
    }
}
