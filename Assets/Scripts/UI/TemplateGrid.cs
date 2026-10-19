using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clones an inactive template child into a grid of cells, on whole pixels: weapon rows, item slots...
/// Edit the template to change every cell; change columns / cell size / max rows to change the layout.
/// The template's position is the first cell.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TemplateGrid : MonoBehaviour
{
    public RectTransform template;
    public Vector2Int cellSize = new Vector2Int(20, 20);
    [Min(1)] public int columns = 1;
    [Tooltip("If above 0, extra columns are added so no column has more than this many rows (filled top to bottom).")]
    public int maxRows = 0;

    public readonly List<RectTransform> cells = new();
    private readonly List<Vector2Int> positions = new();   // (column, row) of each cell

    public int Columns { get; private set; } = 1;
    public int Rows { get; private set; }

    /// <summary>Destroys the previous cells and creates `count` new ones.</summary>
    public void Fill(int count)
    {
        foreach (var cell in cells)
        {
            if (cell == null) continue;
            cell.gameObject.SetActive(false);   // Destroy happens at the end of the frame; don't count it in this frame's layout
            Destroy(cell.gameObject);
        }
        cells.Clear();
        positions.Clear();
        if (template == null) return;

        template.gameObject.SetActive(false);
        bool columnMajor = maxRows > 0;
        Columns = columnMajor ? Mathf.Max(1, Mathf.CeilToInt(count / (float)maxRows)) : Mathf.Max(1, columns);
        Rows = columnMajor ? Mathf.Min(count, maxRows) : Mathf.CeilToInt(count / (float)Columns);

        Vector2 origin = template.anchoredPosition;
        for (int i = 0; i < count; i++)
        {
            int col = columnMajor ? i / maxRows : i % Columns;
            int row = columnMajor ? i % maxRows : i / Columns;
            var cell = Instantiate(template, template.parent);
            cell.name = $"{template.name.Replace(" Template", "")} {i + 1}";
            cell.gameObject.SetActive(true);
            cell.anchoredPosition = origin + new Vector2(col * cellSize.x, -row * cellSize.y);
            cells.Add(cell);
            positions.Add(new Vector2Int(col, row));
        }
    }

    public Vector2Int CellPosition(int index) => positions[index];
}
