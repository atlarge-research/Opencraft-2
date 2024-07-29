
Run server deployment
./Opencraft.exe -deploymentID 0 -logStats -statsFile stats/output_.csv
Run simulation clients
./Opencraft.exe -remoteConfig -deploymentID 1 -userID 1 -nographics -batchmode -duration 60

./Opencraft.exe -deploymentID 0 -deploymentJson ../stats/deployment1.json -logStats -statsFile ../stats/output1b.csv -duration 120
./Opencraft.exe -deploymentID 0 -deploymentJson ../stats/deployment5.json -logStats -statsFile ../stats/output5b.csv -duration 120
./Opencraft.exe -deploymentID 0 -deploymentJson ../stats/deployment10.json -logStats -statsFile ../stats/output10b.csv -duration 120

./Opencraft.exe -remoteConfig -deploymentID 1 -userID 1 -nographics -batchmode -duration 60


Data:
Numbers indicate how many simulated clients
a. Logic system with logic generation but no clocks/inputs (1 layer)
b. Logic system with logic generation and clocks/inputs (1 layer)
c. Normal terrain generation
d. Normal terrain generation + logic generation
e. Logic system 


-deploymentID 0 -terrainType 1-Layer -userID 0 -logStats -statsFile stats/temp.csv -duration 60

./Opencraft.exe -remoteConfig -deploymentID 1 -userID 1 -nographics -batchmode -duration 30
./Opencraft.exe -remoteConfig -deploymentID 1 -userID 1
./Opencraft.exe -remoteConfig -deploymentID 2 -userID 2