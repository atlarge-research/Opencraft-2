"""
This script loads, processes, and visualizes a set of Opencraft 2 CSV metric files organized
with the following directory structure:
| visualization_example.py
| metrics\
|   | client\
|   |    | stat.csv
|   | server\
|   |    | stat.csv
"""

import pandas as pd
import os
import plotly.express as px
import plotly.graph_objects as go
import numpy as np
from plotly.subplots import make_subplots
import time

# from utility import mathjax_fix, transform_df
from kaleido.scopes.plotly import PlotlyScope

scope = PlotlyScope()
template = "plotly_white"
scope.mathjax = None

# Paths containing data files
experiment_path = "./metrics/"
client_path = experiment_path + "client/"
server_path = experiment_path + "server/"

# List of dataframes from all instances
dfs = []

# Colors for graphing
base_color = ["red", "green", "blue"]
medium_color = ["#ff6565", "#65ff65", "#6565ff"]
light_color = ["#ffcccc", "#ccffcc", "#ccccff"]

# ======= FILL THESE MANUALLY BEFORE RUNNING ===========
# Offset values for aligning traces
server_transform_ms = -18000
client_transform_ms = -500

# Duration of the experiment
x_start = 0
x_end = 180

# Cloud rendering details if used
cloud_rendered = False
cloud_render_start = 0
cloud_render_end = 0

# Scale override for graph y axis
frame_time_y_axis = 40
fps_y_axis = 1000 / frame_time_y_axis

# Whether to ignore cached values and recompute, such as when changing any of the above values
remake_cache = False
# ======= END ===========


# Process input data
def process_df(
    df: pd.DataFrame,
    x_end: int,
    ms_time_offset=0,
    cloud_render_start=0,
    cloud_render_end=0,
):
    df["Frame Time [ms]"] = df["Main Thread"] / 1000000  # ns to ms
    df["Elapsed"] = df["Main Thread"].cumsum()  # elapsed time in ns
    if ms_time_offset != 0:
        df["Elapsed"] = df["Elapsed"] + (
            ms_time_offset * 1000000
        )  # used to align traces
        df = df[(df["Elapsed"] > 0)]

    # Convert elapsed time to timestamps to support time-based rolling statistics
    df["Timestamp"] = pd.to_datetime(df["Elapsed"], origin="unix", unit="ns")
    df["Elapsed [s]"] = df["Elapsed"] / 1000000000  # ns to s
    df.set_index("Timestamp", inplace=True, drop=False)

    # Trim x axis
    df = df[df["Elapsed [s]"] <= x_end]

    # Compute Frame Time summary statistics
    df["Mean"] = df["Frame Time [ms]"].rolling(window="1s").mean()
    df["FPS"] = df["Frame Time [ms]"].rolling(window="1s").count()
    df["50th Percentile"] = df["Frame Time [ms]"].rolling(window="1s").quantile(0.50)
    df["95th Percentile"] = df["Frame Time [ms]"].rolling(window="1s").quantile(0.95)
    df["5th Percentile"] = df["Frame Time [ms]"].rolling(window="1s").quantile(0.05)

    df["System Used Memory"] = df["System Used Memory"].apply(
        lambda x: x / 1000000000
    )  # To GB
    df["GC Reserved Memory"] = df["GC Reserved Memory"].apply(
        lambda x: x / 1000000000
    )  # To GB
    df["Total Reserved Memory"] = df["Total Reserved Memory"].apply(
        lambda x: x / 1000000000
    )  # To GB

    #
    df.loc[df["Elapsed [s]"] > cloud_render_end, "Multiplay BitRate In"] = 0
    df.loc[df["Elapsed [s]"] > cloud_render_end, "Multiplay BitRate Out"] = 0
    df["Multiplay BitRate In"] = df["Multiplay BitRate In"].apply(
        lambda x: x / 1000
    )  # To mbps
    df["Multiplay BitRate Out"] = df["Multiplay BitRate Out"].apply(
        lambda x: x / 1000
    )  # To mbps
    # Compute BitRate summary statistics
    df["Mean BitRate In"] = df["Multiplay BitRate In"].rolling(window="1s").mean()
    df["Mean BitRate Out"] = df["Multiplay BitRate Out"].rolling(window="1s").mean()

    # Filter out irrelevant statistics depending on world type
    for ntype in df["Type"].unique():
        if ntype == "Local Client":
            df.loc[
                (df["Elapsed [s]"] <= cloud_render_end)
                & (df["Elapsed [s]"] >= cloud_render_start),
                "NFE RTT",
            ] = 0
        if ntype == "Cloud Client":
            df.loc[
                (df["Elapsed [s]"] >= cloud_render_end)
                & (df["Elapsed [s]"] <= cloud_render_start),
                "NFE RTT",
            ] = 0
        if ntype == "Server":
            df["Number of Players"] = df["Number of Players (Server)"]
            df["Number of Terrain Areas"] = df["Number of Terrain Areas (Server)"]
        if ntype.startswith("Client"):
            df["Number of Players"] = df["Number of Players (Client)"]
            df["Number of Terrain Areas"] = df["Number of Terrain Areas (Client)"]

    # Compute RTT summary statistics
    df["Mean RTT"] = df["NFE RTT"].rolling(window="1s").mean()

    return df


cache_found = False
# Check if there is a cached version of the statistics
if os.path.isdir(experiment_path):
    for filename in os.listdir(experiment_path):
        if filename == "aggregate.pkl":
            cache_found = True

if cache_found and not remake_cache:
    df = pd.read_pickle(experiment_path + "aggregate.pkl")
else:
    # Read server
    if os.path.isdir(server_path):
        for server_file in os.listdir(server_path):
            current_server_path = server_path + server_file
            if not os.path.isdir(current_server_path) and current_server_path.endswith(
                ".csv"
            ):
                print(f"Reading server data {current_server_path}")
                df = pd.read_csv(current_server_path, sep=";", dtype="Int64")
                # Set Type
                df["Type"] = "Server"
                # Process
                # df = transform_df(df, x_end=x_end, ms_time_offset= server_transform_ms, cloud_render_start=cloud_render_start, cloud_render_end=cloud_render_end)

                dfs.append(df)

    # Read clients
    if os.path.isdir(client_path):
        for client_file in os.listdir(client_path):
            current_client_path = client_path + client_file
            if not os.path.isdir(current_client_path) and current_client_path.endswith(
                ".csv"
            ):
                print(f"Reading client data {current_client_path}")
                df = pd.read_csv(current_client_path, sep=";", dtype="Int64")
                # Set Type
                df["Type"] = "Local Client"
                # Process
                # df = transform_df(df, x_end=x_end, ms_time_offset= client_transform_ms, cloud_render_start=cloud_render_start, cloud_render_end=cloud_render_end)

                dfs.append(df)
    # Combine processed metrics from all instances
    df = pd.concat(dfs)
    # Save combined metrics to a cache file
    df.to_pickle(experiment_path + "aggregate.pkl")


def plot_FPS():
    # Plotly express line graph of FPS over time for all instance types
    fig = px.line(
        df,
        # x="Elapsed [s]", y="FPS",
        color="Type",
        color_discrete_sequence=base_color,
        template=template,
        facet_col="Type",
        facet_col_spacing=0.03,
    )

    # Make the lines more legible
    fig.update_traces(marker=dict(size=3), line=dict(width=2))
    # Set X axis properties
    fig.update_xaxes(
        title="Elapsed Time [s]",
        tickmode="linear",
        tick0=0,
        dtick=30,
        range=[x_start, x_end],
    )
    # Set graph layout
    fig.update_layout(
        yaxis=dict(range=[0, fps_y_axis], title="FPS"),
        legend=dict(
            title=None,
            orientation="h",
            y=1,
            yanchor="bottom",
            x=0.5,
            xanchor="center",
            itemsizing="constant",
        ),
        autosize=False,
        width=800,
        height=200,
        margin=dict(l=0, r=0, t=20, b=0),
    )
    # Format facet name annotations
    fig.update_annotations(font_size=14)
    fig.for_each_annotation(lambda a: a.update(text=a.text.split("=")[-1]))

    fig.add_annotation(
        text="Higher is better",
        font=dict(color="red", size=10),
        textangle=-90,
        showarrow=True,
        arrowcolor="red",
        arrowhead=1,
        x=0,
        y=0.40,
        ax=0,
        ay=50,
        xref="paper",
        yref="paper",
        xshift=-25,
    )

    fig.write_image(f"{experiment_path}FPS.pdf")


# Fix for annoying "MathJax library missing" overlay
def mathjax_fix():
    fig = go.Figure(data=go.Scatter(x=[0, 1], y=[1, 2]))
    fig.write_image(f"./mathjax_fix.pdf")
    time.sleep(0.5)


mathjax_fix()
plot_FPS()
