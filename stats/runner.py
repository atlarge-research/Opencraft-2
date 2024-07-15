import os
import time
import subprocess
import shared_config


def get_deployment_string(num_simulated):
    return f"""
        {{
            "nodes": [
                {{
                    "nodeID": 0,
                    "worldConfigs": [
                        {{
                            "worldName": "GameServer",
                            "worldType": "Server",
                            "initializationMode": "Connect",
                            "serverNodeID": 0
                        }}
                    ]
                }},
                {{
                    "nodeID": 1,
                    "worldConfigs": [
                        {{
                            "worldName": "SimulatorClientTest",
                            "worldType": "SimulatedClient",
                            "initializationMode": "Connect",
                            "serverNodeID": 0,
                            "numSimulatedClients": {num_simulated}
                        }}
                    ]
                }}
            ],
            "experimentActions": []
        }}
        """


build_path = "../Builds/"
os.chdir(build_path)

executable_path = f"./Opencraft.exe"
stats_path = "../stats/"
data_path = f"{stats_path}Data/"
jsons_path = f"{stats_path}jsons/"

if not os.path.exists(data_path):
    os.makedirs(data_path)
if not os.path.exists(jsons_path):
    os.makedirs(jsons_path)

config = shared_config.config
tolerance = config["tolerance"]
sim_clients_duration = config["sim_clients_duration"]
num_simulated_options = config["num_sims"]
iterations = config["iterations"]
terrain_types = config["terrainTypes"]


server_duration = sim_clients_duration + tolerance
sim_clients_command = f"{executable_path} -remoteConfig -deploymentID 1 -userID 1 -nographics -batchmode -duration {sim_clients_duration}"

for iteration in range(iterations):
    for identifier, terrain_type in terrain_types.items():
        for num_sim in num_simulated_options:
            print(f"Running iteration {iteration} of {terrain_type} with {num_sim} simulated client{'s'if num_sim > 1 else ''}")
            deployment_string = get_deployment_string(num_sim)
            deployment_path = f"{jsons_path}deployment{num_sim}.json"
            output_path = f"{data_path}raw-{identifier}-{num_sim}-{iteration}.csv"

            if os.path.exists(output_path):
                print(f"Output file {output_path} already exists. Skipping.")
                continue

            with open(deployment_path, "w") as f:
                f.write(deployment_string)

            server_command = f"{executable_path} -deploymentID 0 -deploymentJson {deployment_path} -duration {server_duration} -logStats -statsFile {output_path} -terrainType {terrain_type}"
            print("Running server")
            server_process = subprocess.Popen(server_command, shell=True)
            time.sleep(tolerance)

            print("Running simulated clients")
            clients_process = subprocess.Popen(sim_clients_command, shell=True)
            clients_process.wait()
            print("Simulated clients done")
            server_process.wait()
            print("Server Done")
            print("-"*20)
