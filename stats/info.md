
Run server deployment
./Opencraft.exe -deploymentID 0 -logStats -statsFile stats/output_.csv
Run simulation clients
./Opencraft.exe -remoteConfig -deploymentID 1 -userID 1 -nographics -batchmode -duration 60



Data:
Numbers indicate how many simulated clients
a. Logic system with logic generation but no clocks/inputs (1 layer)
b. Logic system with logic generation and clocks/inputs (1 layer)
c. Normal terrain generation
d. Normal terrain generation + logic generation
e. Logic system 