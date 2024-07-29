import pandas as pd
import os
import shared_config

data_path = "Data/"
if not os.path.exists(data_path):
    exit("Data folder does not exist. Please run stats/runner.py first.")

os.chdir(data_path)
csv_files = os.listdir()
csv_files = [file for file in csv_files if file.startswith("raw")]

for input_file in csv_files:
    output_file = input_file.replace("raw-", "formatted-")
    sheet_name = input_file.split('.')[0]
    table = pd.read_csv(input_file, header=None)
    for i in range(0, len(table)):
        values = table.iloc[i, 0].split(';')[:-1]
        for j in range(len(values)):
            table.at[i, j] = values[j]
    df_cleaned = table[table.iloc[:, -1] != "0"]
    df_cleaned.to_csv(output_file, index=False, header=False)