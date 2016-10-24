while [ true ]; do
    printf "$(free | grep /+ | awk '{print $4}')\n$(free | grep Mem | awk '{print $2}')" > "$1"
    sleep 5
done