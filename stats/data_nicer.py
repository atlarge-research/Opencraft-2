import pandas as pd

additions = ["1a", "1b", "5a", "5b", "10a", "10b"]

for addtion in additions:

    input_file = f'output{addtion}.csv'
    sheet_name = input_file.split('.')[0]
    output_file = f'formatted{addtion}.csv'

    table = pd.read_csv(input_file, header=None)
    # headers = table.iloc[0,0].split(';')


    for i in range(0, len(table)):
        values = table.iloc[i, 0].split(';')[:-1]
        for j in range(len(values)):
            table.at[i, j] = values[j]
            

    df_cleaned = table[table.iloc[:, -1] != "0"]
    df_cleaned.to_csv(output_file, index=False, header=False)

