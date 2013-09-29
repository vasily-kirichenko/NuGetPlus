module NuGetPlus.BatchOperations

open System.IO
open System.Collections.Concurrent
open NuGet

type PackageInRepo = 
    { RepoPath: string
      Package: RestorePackage
      Projects: List<string> }

type RepositoryInfo = 
    { RepoPath: RepositoryPath
      Manager: PackageManager
      Queue: BlockingCollection<RestorePackage> }

let ScanPackages packages = 
    packages
    |> Seq.groupBy (fun p -> p.Id)
    |> Seq.map (fun (id, packages) -> 
               (id, packages
                    |> Seq.map (fun p -> p.Version)
                    |> Seq.distinct
                    |> Seq.sort))
    |> Seq.filter (fun (id, versions) -> Seq.length versions > 1)

let RestorePackages packages = 
    let repositories = Set.map fst packages
    let managers = 
        repositories 
        |> Seq.map (fun ((RepositoryPath repoPath) as repo) -> 
                repo, { RepoPath = repo
                        Manager = 
                            PhysicalFileSystem (repoPath)
                            |> Settings.LoadDefaultSettings
                            |> GetRawManager repo 
                        Queue = new BlockingCollection<RestorePackage>() })
        |> Map.ofSeq

    packages |> Set.iter (fun (repoPath, package) -> managers.[repoPath].Queue.Add package)

    repositories
    |> Seq.toArray
    |> Array.Parallel.iter (fun repo -> 
               let package = managers.[repo].Queue.Take ()
               managers.[repo].Manager.InstallPackage (package.Id, package.Version, true, true))