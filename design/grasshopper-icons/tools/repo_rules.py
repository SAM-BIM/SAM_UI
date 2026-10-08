"""Repository-specific classification decisions for SAM_UI (the only non-shared tool file).

OVERRIDES     : component display name -> (object glyph, op, extra)   extra: None | "plural" | "library" | note
PARAM_OBJECTS : param type key (Goo<X>Param class or typeof(X) name) -> glyph | (glyph, container, plural)
OBJECTS/VERBS : extra noun/verb rules tried before the shared ones (same shapes as SAM's OBJECTS/VERBS)
"""
OVERRIDES = {
    "SAMAnalytical.PrintAHU": ("ahu", "export", "AHU schedules to an Excel template"),
    "SAMAnalytical.PrintRDS": ("space", "export", "room data sheets"),
    "SAMAnalytical.ShowDiagram": ("mollierChart", "display", None),
    "SAMAnalytical.ShowDiagramAHU": ("ahu", "display", "Mollier diagram of an AHU"),
    "SAMAnalytical.ShowDiagramSpace": ("space", "display", "Mollier diagram of a space"),
    "SAMAnalytical.MultitaskerWorkflow": ("tasks", "run", "batch TAS workflow for many models"),
    "SAMMollier.Geometry ": ("geometry", "convert", "diagram lines, points and processes as Rhino geometry"),
}
PARAM_OBJECTS = {}
OBJECTS = []
VERBS = []
