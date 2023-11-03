module ryktetloper.Drawing

type Coordinate =
    {
        x: float
        y: float
    }
type Line =
    {
        start: Coordinate
        stop: Coordinate
    }

type Drawing =
    {
        lines: Line[][]
        color: string
    }
