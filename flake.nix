{
  description = "flake for running the sota staircase ReStepper";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = nixpkgs.legacyPackages.${system};
      in
      with pkgs; {
        devShells.default = mkShellNoCC {
          buildInputs = [
            git
            git-lfs
            git-filter-repo
            (python313.withPackages (python-pkgs: [
              python-pkgs.tqdm
            ]))
          ];
        };
      }
    );
}