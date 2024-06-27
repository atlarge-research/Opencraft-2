from enum import Enum
from colorama import Fore, Style


class Block(Enum):
    EMPTY = 0
    INPUT = 1
    CLOCK = 2
    WIRE = 3
    LAMP = 4
    AND = 5
    OR = 6
    NOT = 7
    XOR = 8

    def __str__(self):
        return f"{Color.get_color(self)}{self.value}{Style.RESET_ALL}"


class Color(Enum):
    EMPTY = Fore.BLACK
    INPUT = Fore.WHITE
    CLOCK = Fore.RED
    WIRE = Fore.GREEN
    LAMP = Fore.YELLOW
    AND = Fore.BLUE
    OR = Fore.CYAN
    NOT = Fore.MAGENTA
    XOR = Fore.LIGHTRED_EX

    @staticmethod
    def get_color(block):
        return Color[block.name].value


min_side = 0
max_side = 15
total = max_side + 1


def print_grid(grid):
    print("-" * 31)
    for row in grid:
        print(" ".join(str(cell) for cell in row))


def edges(row, col):
    return row == min_side or row == max_side or col == min_side or col == max_side


def strips(row):
    return row % 2 != 0


grid = [[Block.EMPTY for _ in range(total)] for _ in range(total)]


def grid_populator(blockType, addition, condition):
    for row in range(total):
        for col in range(total):
            if condition(row, col, addition):
                grid[row][col] = blockType


grid_populator(Block.INPUT,0,
    lambda row, col, _: (col == min_side or col == max_side) and strips(row) and not row == max_side,
)
grid_populator(Block.CLOCK, 0, lambda row, col, _: (row == 15) and (col == 8))
grid_populator(Block.WIRE, 3,
    lambda row, col, addition: (strips(row)
    and not edges(row, col)
    and (col % 6 != (addition + (row + 1) * 0.5) % 6))
    or (row == max_side  and col != 8),
)
print_grid(grid)
grid_populator(Block.LAMP, 0,
    lambda row, col, _: (
        edges(row, col) and not strips(row) and (col % 3 != 2) or row == min_side
    ),
)
grid_populator(Block.AND, 9,
    lambda row, col, addition: strips(row)
    and not edges(row, col)
    and (col % 14 == (addition + (row + 1) * 0.5) % 14),
)
grid_populator(Block.OR, 3,
    lambda row, col, addition: strips(row)
    and not edges(row, col)
    and (col % 14 == (addition + (row + 1) * 0.5) % 14),
)
print_grid(grid)
grid_populator(Block.NOT, 3.5,
    lambda row, col, addition: not strips(row)
    and not edges(row, col)
    and (col % 6 == (addition + (row + 1) * 0.5) % 6),
)
grid_populator(Block.XOR, 11,
    lambda row, col, addition: strips(row)
    and not edges(row, col)
    and (col % 14 == (addition + (row + 1) * 0.5) % 14),
)
print_grid(grid)
