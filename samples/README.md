# Sample files

Two example spreadsheets with deliberately different layouts and languages, to show that the
importer adapts to whatever format the source is in:

| File | Language | Shape | Notes |
|------|----------|-------|-------|
| `compras-ejemplo-es.xlsx` | Spanish | Arrivals | Has a title banner row above the header, `dd/MM/yyyy` dates, `$` prices. Columns: `Producto`, `Cantidad`, `Precio unitario`, `Fecha`. |
| `sales-example-en.xlsx` | English | Sales | Different headers entirely: `Item`, `Code`, `Units`, `Unit Price`, `Date`. |

Upload either one on the **Import** page. The system reads the columns, proposes a mapping onto the
canonical fields (Product, Quantity, Unit price, Date, …), and asks you to confirm. After you confirm
once, re-uploading a file with the same columns imports automatically.

You can also drop in a **photo of a paper record** (`.jpg`/`.png`) — that path needs an
`ANTHROPIC_API_KEY` (see the root README).
